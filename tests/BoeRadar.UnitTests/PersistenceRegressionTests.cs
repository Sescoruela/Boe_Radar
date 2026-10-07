using BoeRadar.Application;
using BoeRadar.Domain;
using BoeRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using BoeRadar.Web;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using BoeRadar.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;

namespace BoeRadar.UnitTests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BOERADAR_TEST_CONNECTION")))
            Skip = "Set BOERADAR_TEST_CONNECTION to the local test PostgreSQL (localhost:54329).";
    }
}

public sealed class MailpitFactAttribute : FactAttribute
{
    public MailpitFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BOERADAR_TEST_CONNECTION")) ||
            Environment.GetEnvironmentVariable("BOERADAR_TEST_SMTP") != "true")
            Skip = "Opt in with BOERADAR_TEST_SMTP=true and local PostgreSQL/Mailpit. Never uses an external SMTP server.";
    }
}

// Each test migrates an isolated schema. It never changes the application's public schema.
public sealed class PersistenceRegressionTests
{
    [PostgreSqlFact]
    public async Task IntentFilteringRunsBeforeCountPaginationAndPersonalizedRanking()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await using var db = fixture.Open();
        var clock = new TestClock();
        var titles = new[] { "Convocatoria de ayudas para comercio", "Bases de subvenciones para pymes",
            "Ayudas para tecnología", "Cambios fiscales en el impuesto para empresas",
            "Cotización de trabajadores autónomos en la Seguridad Social", "Resolución administrativa sin tema fiscal de la Comisión Española de Ayuda al Refugiado" };
        await new EfIngestionStore(db, clock).ImportAsync(new("BOE", new(2026, 10, 1), new Uri("https://www.boe.es/"),
            titles.Select((title, index) => new OfficialDocument($"BOE-A-2026-{index + 1:00000}", "normal",
                index < 3 ? "5B" : "1", "Sección", "1", "Ministerio", null,
                title, null, null, null, null)).ToArray()), "manual");
        var catalog = new EfPublicationCatalog(db, clock);
        var first = await catalog.SearchAsync(new(null, null, null, null, 1, 2, Intent: "grants"));
        Assert.Equal(3, first.TotalItems);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(2, first.Items.Count);
        var second = await catalog.SearchAsync(new(null, null, null, null, 2, 2, Intent: "grants"));
        Assert.Single(second.Items);
        Assert.DoesNotContain(second.Items[0].Id, first.Items.Select(item => item.Id));
        var tax = await catalog.SearchAsync(new(null, null, null, null, Intent: "tax"));
        Assert.Equal(2, tax.TotalItems); // Mentions are navigation hints, including a negated mention.
        var obligations = await catalog.SearchAsync(new(null, null, null, null, Intent: "obligations"));
        Assert.Single(obligations.Items);
        var narrowed = await catalog.SearchAsync(new("tecnología", new(2026, 10, 1), new(2026, 10, 1), "5B", Intent: "grants"));
        Assert.Single(narrowed.Items);
        var wrongSection = await catalog.SearchAsync(new(null, null, null, "1", Intent: "grants"));
        Assert.Empty(wrongSection.Items);
        var personalized = await new PersonalizedPublicationSearch(catalog).ExecuteAsync(
            new("sme", "retail", "baleares"), new(null, null, null, null, PageSize: 1, Intent: "grants"));
        Assert.Equal(3, personalized.TotalItems);
        Assert.Equal(3, personalized.CatalogSignalCount);
        Assert.Contains("comercio", Assert.Single(personalized.Items).Title);
        await Assert.ThrowsAsync<ArgumentException>(() => catalog.SearchAsync(new(null, null, null, null, Intent: "invalid")));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("grants", true)]
    [InlineData("tax", true)]
    [InlineData("obligations", true)]
    [InlineData("Grant", false)]
    [InlineData("unknown", false)]
    public void SearchIntentHasAnExplicitAllowlist(string? intent, bool valid) =>
        Assert.Equal(valid, PublicationIntents.IsValid(intent));

    [PostgreSqlFact]
    public async Task SubscriptionProfileMigrationPreservesExistingSubscriptionWithoutOptingItIn()
    {
        await using var fixture = await TestDatabase.CreateAsync("20261004124228_AddReviewObservationsAndRefreshProgress");
        await using var db = fixture.Open();
        var now = new TestClock().Now;
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO subscriptions (id, email, status, verification_token_hash, verification_expires_at,
                management_token_hash, categories, keywords, timezone, digest_hour, consented_at, created_at, updated_at)
            VALUES ({id}, 'legacy@example.invalid', 'Active', '', {now}, 'legacy-manage', '[]'::jsonb,
                '[]'::jsonb, 'Europe/Madrid', 8, {now}, {now}, {now})
            """);
        await db.Database.MigrateAsync();
        var view = await new EfSubscriptionStore(db, new TestClock()).GetAsync("legacy-manage", default);
        Assert.Equal("legacy@example.invalid", view!.Email);
        Assert.Null(view.Preferences.Profile);
        Assert.Equal(id, (await db.Subscriptions.SingleAsync()).Id);
    }

    [PostgreSqlFact]
    public async Task DigestEvidenceRankingUsesOnlyReviewOfTheSameSourceHashAsAnalysis()
    {
        foreach (var sameHash in new[] { true, false })
        {
            await using var fixture = await TestDatabase.CreateAsync();
            await using var db = fixture.Open();
            var clock = new TestClock();
            await ImportAsync(db, clock, 2);
            var documents = await db.SourceDocuments.OrderBy(item => item.ExternalId).ToArrayAsync();
            foreach (var document in documents)
                db.DocumentAnalyses.Add(DocumentAnalysis.Create(document.Id, new string('a', 64), true,
                    RadarCategory.Grant, "Resumen", "[]", "[]", "[]", .9m, "gemini", "fixture", "radar-v2", null, null, clock.Now));
            var subscription = Subscription.Create("test@example.invalid", "verify", clock.Now.AddHours(24), "[]", "[]", clock.Now,
                JsonSerializer.Serialize(new BusinessProfile("sme", "retail", "baleares"), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            subscription.Activate("manage", clock.Now);
            db.Subscriptions.Add(subscription);
            await db.SaveChangesAsync();
            var reviews = new EfSourceReviewStore(db, clock);
            await reviews.SaveAsync(documents[1].ExternalId, new(new string(sameHash ? 'a' : 'b', 64), clock.Now,
                [new("recipients", "Destinatarios", ["Podrán solicitar las ayudas las pymes de comercio en Illes Balears."])]));
            var store = new EfSubscriptionStore(db, clock, reviews);
            Assert.Equal(1, (await store.QueueDigestsAsync(new(2026, 10, 1), new("https://example.invalid"), false, clock.Now, default)).DigestsQueued);
            var body = (await db.OutboxMessages.AsNoTracking().SingleAsync()).Body;
            Assert.Equal(sameHash, body.Contains("Los fragmentos oficiales", StringComparison.Ordinal));
            var first = body.IndexOf("id=" + documents[0].ExternalId, StringComparison.Ordinal);
            var second = body.IndexOf("id=" + documents[1].ExternalId, StringComparison.Ordinal);
            Assert.True(sameHash ? second < first : first < second);
        }
    }

    [MailpitFact]
    public async Task PersonalizedAlertFlowDeliversOnlyToLocalMailpitAndDoesNotRepeatDigest()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await using var db = fixture.Open();
        var clock = new TestClock();
        var store = new EfSubscriptionStore(db, clock);
        var service = new SubscriptionService(store, clock);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Host"] = "127.0.0.1",
            ["Email:Port"] = "1025",
            ["Email:From"] = "radar@example.invalid",
            ["Email:EnableSsl"] = "false"
        }).Build();
        var delivery = new DigestService(store, new SmtpEmailSender(config), clock);
        var profile = new BusinessProfile("sme", "retail", "baleares");
        var baseUri = new Uri("http://localhost:4200");
        await service.RegisterAsync($"flow-{Guid.NewGuid():N}@example.invalid", new([], [], 8, profile), baseUri, default);
        var verification = await db.OutboxMessages.AsNoTracking().SingleAsync();
        var token = System.Text.RegularExpressions.Regex.Match(verification.Body, @"#verify=([a-f0-9]{64})").Groups[1].Value;
        Assert.Equal(new DispatchResult(1, 0), await delivery.DispatchAsync(1, default));
        var managementToken = await service.VerifyAsync(token, baseUri, default);
        Assert.NotNull(managementToken);
        Assert.Equal(new DispatchResult(1, 0), await delivery.DispatchAsync(1, default));
        Assert.Equal(profile, (await service.GetAsync(managementToken, default))!.Preferences.Profile);
        await ImportAsync(db, clock, 1);
        var document = await db.SourceDocuments.SingleAsync();
        db.DocumentAnalyses.Add(DocumentAnalysis.Create(document.Id, new string('a', 64), true,
            RadarCategory.Grant, "Resumen de prueba; no es un análisis de Gemini real", "[]", "[]", "[]", .9m,
            "gemini", "fixture", "radar-v2", null, null, clock.Now));
        await db.SaveChangesAsync();
        Assert.Equal(1, (await delivery.QueueAsync(new(2026, 10, 1), baseUri, false, default)).DigestsQueued);
        Assert.Equal(new DispatchResult(1, 0), await delivery.DispatchAsync(1, default));
        Assert.Equal(0, (await delivery.QueueAsync(new(2026, 10, 1), baseUri, false, default)).DigestsQueued);
        Assert.Equal(new DispatchResult(0, 0), await delivery.DispatchAsync(1, default));
        Assert.Equal(3, await db.OutboxMessages.CountAsync(item => item.Status == DeliveryStatus.Sent && item.Body == ""));
        Assert.NotNull((await db.AlertDigests.AsNoTracking().SingleAsync()).SentAt);
        Assert.True(await service.UnsubscribeAsync(managementToken, default));
        Assert.Null(await service.GetAsync(managementToken, default));
    }

    [PostgreSqlFact]
    public async Task AlertProfileSurvivesVerificationAndCannotBeOverwrittenByPublicReregistration()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await using var db = fixture.Open();
        var clock = new TestClock();
        var store = new EfSubscriptionStore(db, clock);
        var profile = new BusinessProfile("sme", "retail", "baleares");
        await store.RegisterAsync("test@example.invalid", new([], [], 9, profile), "verify", "https://example.invalid/#verify=test", clock.Now, default);
        Assert.NotNull((await db.Subscriptions.SingleAsync()).BusinessProfileJson);
        Assert.NotNull(await store.VerifyAsync("verify", "manage", "https://example.invalid/#manage=test", clock.Now, default));
        Assert.Equal(profile, (await store.GetAsync("manage", default))!.Preferences.Profile);
        await store.RegisterAsync("test@example.invalid", new([], [], 8, new("autonomous", "other", "all")),
            "attack", "https://example.invalid", clock.Now.AddHours(2), default);
        Assert.Equal(profile, (await store.GetAsync("manage", default))!.Preferences.Profile);
        Assert.True(await store.UpdateAsync("manage", new([], [], 10), clock.Now, default));
        Assert.Null((await store.GetAsync("manage", default))!.Preferences.Profile);
        Assert.Equal(10, (await store.GetAsync("manage", default))!.Preferences.DigestHour);
    }

    [PostgreSqlFact]
    public async Task PersonalizedDigestUsesOnlyLatestAnalysisIsIdempotentAndCancelsWhenProfileRemoved()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await using var db = fixture.Open();
        var clock = new TestClock();
        await new EfIngestionStore(db, clock).ImportAsync(new("BOE", new(2026, 10, 1), new Uri("https://www.boe.es/"),
            Enumerable.Range(1, 5).Select(index => new OfficialDocument($"BOE-A-2026-{index:00000}", "1", "1", "General",
                "1", "Ministerio", null, index == 2 ? "Ayudas al comercio para pymes" : "Cambios generales", null,
                null, null, null)).ToArray()), "manual");
        var documents = await db.SourceDocuments.OrderBy(item => item.ExternalId).ToArrayAsync();
        for (var index = 0; index < documents.Length; index++)
        {
            db.DocumentAnalyses.Add(DocumentAnalysis.Create(documents[index].Id, new string('a', 64), true,
                RadarCategory.Grant, "Resumen", "[]", "[]", "[]", .9m, "gemini", "fixture", "radar-v2", null, null, clock.Now.AddHours(-1)));
            if (index >= 2)
                db.DocumentAnalyses.Add(DocumentAnalysis.Create(documents[index].Id, new string('b', 64), index != 2,
                    RadarCategory.Grant, "Actual", "[]", "[]", "[]", .9m, index == 4 ? "heuristic" : "gemini", "fixture",
                    index == 3 ? "radar-v1" : "radar-v2", null, null, clock.Now));
        }
        var subscription = Subscription.Create("test@example.invalid", "verify", clock.Now.AddHours(24), "[]", "[]", clock.Now);
        subscription.Activate("manage", clock.Now);
        subscription.UpdatePreferences("[]", "[]", 8, clock.Now,
            JsonSerializer.Serialize(new BusinessProfile("sme", "retail", "baleares"), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync();
        var store = new EfSubscriptionStore(db, clock);
        var plan = await store.QueueDigestsAsync(new(2026, 10, 1), new("https://example.invalid"), false, clock.Now, default);
        Assert.Equal(1, plan.DigestsQueued);
        Assert.Equal(2, plan.Matches); // Unknown profile scope remains included; superseded positives do not.
        var message = await db.OutboxMessages.AsNoTracking().SingleAsync();
        Assert.True(message.Body.IndexOf("Ayudas al comercio", StringComparison.Ordinal) < message.Body.IndexOf("• Cambios generales", StringComparison.Ordinal));
        Assert.Contains("No confirma elegibilidad", message.Body);
        Assert.Contains("Alcance por comprobar", message.Body);
        Assert.Contains("/#unsubscribe=", message.Body);
        Assert.Equal(0, (await store.QueueDigestsAsync(new(2026, 10, 1), new("https://example.invalid"), false, clock.Now, default)).DigestsQueued);
        Assert.Equal(2, await db.AlertMatches.CountAsync());
        var claimed = Assert.Single(await store.ClaimMessagesAsync(1, clock.Now, default));
        Assert.True(await store.UpdateAsync("manage", new([], []), clock.Now, default));
        Assert.False(await store.CanDeliverAsync(claimed.Id, claimed.AttemptCount, default));
        var canceled = await db.OutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(DeliveryStatus.Canceled, canceled.Status);
        Assert.Empty(canceled.Body);
        Assert.Null((await db.Subscriptions.AsNoTracking().SingleAsync()).BusinessProfileJson);
    }

    [PostgreSqlFact]
    public async Task UnsubscribeCancelsQueuedDigestAndClearsProfileAndManagementAccess()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await using var db = fixture.Open();
        var clock = new TestClock();
        var subscription = Subscription.Create("test@example.invalid", "verify", clock.Now.AddHours(24), "[]", "[]", clock.Now, "{}");
        subscription.Activate("manage", clock.Now);
        var digest = AlertDigest.Create(subscription.Id, new(2026, 10, 1), 1, "unsubscribe", clock.Now);
        db.Subscriptions.Add(subscription);
        db.AlertDigests.Add(digest);
        db.OutboxMessages.Add(OutboxMessage.Create("digest", "test-digest", subscription.Email, "Test", "Profile data", digest.Id, clock.Now));
        await db.SaveChangesAsync();
        var store = new EfSubscriptionStore(db, clock);
        Assert.True(await store.UnsubscribeAsync("unsubscribe", clock.Now, default));
        Assert.False(await store.UnsubscribeAsync("unsubscribe", clock.Now, default));
        Assert.Null(await store.GetAsync("manage", default));
        Assert.Null((await db.Subscriptions.AsNoTracking().SingleAsync()).BusinessProfileJson);
        var canceled = await db.OutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(DeliveryStatus.Canceled, canceled.Status);
        Assert.Empty(canceled.Body);
    }

    [PostgreSqlFact]
    public async Task MigratedDatabaseMatchesModelSnapshotAndHasNoPendingMigrations()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await using var db = fixture.Open();
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [PostgreSqlFact]
    public async Task PersonalizedSearchUsesFourQueriesForThousandCandidatesAndKeepsPublicPageLimit()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var counter = new QueryCounter();
        await using var db = fixture.Open(counter);
        var clock = new TestClock();
        await new EfIngestionStore(db, clock).ImportAsync(new("BOE", new(2026, 10, 1), new Uri("https://www.boe.es/"),
            Enumerable.Range(1, 1001).Select(index => new OfficialDocument($"BOE-B-2026-{index:00000}", "1",
                "5B", "Otros anuncios oficiales", "1", "Ministerio", null, "Ayudas para pymes", null,
                null, null, null)).ToArray()), "manual");
        var first = await db.SourceDocuments.OrderBy(item => item.ExternalId).FirstAsync();
        db.DocumentAnalyses.Add(DocumentAnalysis.Create(first.Id, new string('a', 64), true, RadarCategory.Grant,
            "Anterior", "[]", "[]", "[]", .8m, "heuristic", "fixture", "radar-v2", null, null, clock.Now.AddDays(-1)));
        db.DocumentAnalyses.Add(DocumentAnalysis.Create(first.Id, new string('b', 64), true, RadarCategory.Grant,
            "Actual", "[]", "[]", "[]", .8m, "heuristic", "fixture", "radar-v2", null, null, clock.Now));
        await db.SaveChangesAsync();
        var reviews = new EfSourceReviewStore(db, clock);
        await reviews.SaveAsync(first.ExternalId, new(new string('a', 64), clock.Now,
            [new("recipients", "Destinatarios", ["Pymes de comercio en Baleares."])]));
        counter.Commands.Clear();
        var catalog = new EfPublicationCatalog(db, clock);
        var result = await new PersonalizedPublicationSearch(catalog, reviews, clock).ExecuteAsync(
            new("sme", "retail", "baleares"), new(null, null, null, null, 1, 20));
        Assert.Equal(4, counter.Commands.Count);
        Assert.Equal(first.Id, result.Items[0].Id);
        Assert.Equal("Actual", result.Items[0].Analysis!.Summary);
        Assert.Equal(1000, result.TotalItems);
        Assert.Equal(1001, result.CatalogSignalCount);
        Assert.True(result.IsPartial);
        Assert.Equal(1, result.EvidenceReviewedCount);
        Assert.Equal(clock.Now, result.EvidenceAsOf);
        var analysisQuery = Assert.Single(counter.Commands, command => command.Contains("document_analyses"));
        Assert.Contains("ROW_NUMBER()", analysisQuery); // Latest per document is chosen in PostgreSQL, not in memory.
        Assert.DoesNotContain("requirements", analysisQuery);
        Assert.DoesNotContain("deadlines", analysisQuery);
        Assert.DoesNotContain("evidence", analysisQuery);
        var publicPage = await catalog.SearchAsync(new(null, null, null, null, 1, 1000));
        Assert.Equal(100, publicPage.PageSize);
        Assert.Equal(100, publicPage.Items.Count);
    }

    [PostgreSqlFact]
    public async Task EmptyPersonalizedSearchDoesNotReadAnalysesOrEvidence()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var counter = new QueryCounter();
        await using var db = fixture.Open(counter);
        var clock = new TestClock();
        var result = await new PersonalizedPublicationSearch(new EfPublicationCatalog(db, clock),
            new EfSourceReviewStore(db, clock), clock).ExecuteAsync(new("sme", "other", "all"), new(null, null, null, null));
        Assert.Empty(result.Items);
        Assert.Equal(2, counter.Commands.Count);
        Assert.False(result.IsPartial);
    }

    [PostgreSqlFact]
    public async Task LegacyAnalysisRetainsSummaryButCannotExposeUnvalidatedActionableFacts()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await using var db = fixture.Open();
        var clock = new TestClock();
        await ImportAsync(db, clock, 1);
        var document = await db.SourceDocuments.SingleAsync();
        db.DocumentAnalyses.Add(DocumentAnalysis.Create(document.Id, new string('a', 64), true,
            RadarCategory.Grant, "Resumen anterior", "[\"Requisito sin evidencia\"]",
            "[{\"date\":\"2026-10-15\",\"description\":\"Inventado\",\"isExplicit\":true}]", "[]", .9m,
            "gemini", "fixture", "radar-v1", null, null, clock.Now));
        await db.SaveChangesAsync();
        var detail = await new EfPublicationCatalog(db, clock).GetAsync(document.Id);
        Assert.Equal("Resumen anterior", detail!.Analysis!.Summary);
        Assert.Empty(detail.Analysis.Requirements);
        Assert.Empty(detail.Analysis.Deadlines);
        Assert.Single(await db.DocumentAnalyses.ToArrayAsync()); // Preserve historical records.
    }

    [PostgreSqlFact]
    public async Task MigrationPreservesPreviouslyStoredReviewsAndTheirDates()
    {
        await using var fixture = await TestDatabase.CreateAsync("20261004114953_AddStoredSourceReviews");
        await using var db = fixture.Open();
        var clock = new TestClock();
        var original = new ActionableSourceReview(new string('a', 64), clock.Now, []);
        db.SourceReviews.Add(new StoredSourceReview
        {
            ExternalId = "BOE-A-2026-1",
            SourceHash = original.SourceHash,
            Version = "source-review-v1",
            RecordedAt = clock.Now,
            ReviewJson = JsonSerializer.Serialize(original, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        });
        await db.SaveChangesAsync();
        await db.Database.MigrateAsync();
        var result = await new EfSourceReviewStore(db, clock).GetAsync(["BOE-A-2026-1"], clock.Now);
        Assert.Equal(original.SourceHash, result["BOE-A-2026-1"].SourceHash);
        Assert.Equal(clock.Now, result["BOE-A-2026-1"].ReviewedAt);
        Assert.Single(await db.SourceReviewObservations.ToArrayAsync());
    }

    [PostgreSqlFact]
    public async Task ConcurrentWorkersDoNotClaimSameMessage()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await using var first = fixture.Open();
        await using var second = fixture.Open();
        var clock = new TestClock();
        first.OutboxMessages.Add(OutboxMessage.Create("digest", "concurrent", "test@example.invalid", "Test", "Test", null, clock.Now));
        await first.SaveChangesAsync();
        var results = await Task.WhenAll(new EfSubscriptionStore(first, clock).ClaimMessagesAsync(1, clock.Now, default),
            new EfSubscriptionStore(second, clock).ClaimMessagesAsync(1, clock.Now, default));
        Assert.Single(results.SelectMany(items => items));
        Assert.Equal(1, (await first.OutboxMessages.AsNoTracking().SingleAsync()).AttemptCount);
    }
    [PostgreSqlFact]
    public async Task RefreshRecoversLongSleepRetriesGapAndRechecksExistingDates()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var clock = new TestClock();
        await using (var db = fixture.Open())
        {
            await new EfIngestionStore(db, clock).ImportAsync(new("BOE", new(2026, 9, 1),
                new Uri("https://www.boe.es/"), [new("BOE-A-2026-1", "1", "1", "General", "1",
                    "Ministerio", null, "Ayudas", null, null, null, null)]), "manual");
        }
        var source = new RefreshSource { FailDate = new(2026, 9, 15) };
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(clock);
        services.AddScoped(_ => fixture.Open());
        services.AddSingleton<IOfficialGazetteSource>(source);
        services.AddScoped<IIngestionStore, EfIngestionStore>();
        services.AddScoped<ImportOfficialIssue>();
        await using var provider = services.BuildServiceProvider();
        var refresh = new CatalogRefreshService(provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CatalogRefreshService>.Instance, clock);
        await refresh.RefreshSafelyAsync(default);
        await using (var db = fixture.Open())
        {
            Assert.Equal(source.FailDate, (await db.CatalogRefreshProgress.SingleAsync()).NextDate);
            Assert.True(await db.SourceDocuments.AnyAsync(item => item.PublicationDate == new DateOnly(2026, 10, 4)));
        }
        source.FailDate = null;
        await refresh.RefreshSafelyAsync(default);
        await using (var db = fixture.Open())
        {
            Assert.Equal(new DateOnly(2026, 10, 5), (await db.CatalogRefreshProgress.SingleAsync()).NextDate);
            Assert.Equal(2, await db.SourceDocuments.CountAsync(item => item.PublicationDate == new DateOnly(2026, 10, 4)));
            Assert.True(await db.SourceDocuments.AnyAsync(item => item.PublicationDate == new DateOnly(2026, 9, 15)));
        }
    }

    [PostgreSqlFact]
    public async Task ReviewSequenceAToBToARetainsCurrentAndHistoricalViews()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await using var db = fixture.Open();
        var clock = new TestClock();
        var store = new EfSourceReviewStore(db, clock);
        var first = new ActionableSourceReview(new string('a', 64), clock.Now, []);
        await store.SaveAsync("BOE-A-2026-1", first);
        var atA = clock.Now;
        clock.Now = clock.Now.AddMinutes(1);
        var second = first with { SourceHash = new string('b', 64), ReviewedAt = clock.Now };
        await store.SaveAsync("BOE-A-2026-1", second);
        var atB = clock.Now;
        clock.Now = clock.Now.AddMinutes(1);
        await store.SaveAsync("BOE-A-2026-1", first with { ReviewedAt = clock.Now });
        Assert.Equal(first.SourceHash, (await store.GetAsync(["BOE-A-2026-1"], clock.Now))["BOE-A-2026-1"].SourceHash);
        Assert.Equal(second.SourceHash, (await store.GetAsync(["BOE-A-2026-1"], atB))["BOE-A-2026-1"].SourceHash);
        Assert.Equal(first.SourceHash, (await store.GetAsync(["BOE-A-2026-1"], atA))["BOE-A-2026-1"].SourceHash);
        Assert.Equal(2, await db.SourceReviews.CountAsync());
        Assert.Equal(3, await db.SourceReviewObservations.CountAsync());
        await store.SaveAsync("BOE-A-2026-1", first with { ReviewedAt = clock.Now });
        Assert.Equal(3, await db.SourceReviewObservations.CountAsync());
    }

    [PostgreSqlFact]
    public async Task VeryLargeCatalogPageReturnsEmptyWithoutOverflow()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await using var db = fixture.Open();
        await ImportAsync(db, new TestClock(), 1);
        var result = await new EfPublicationCatalog(db, new TestClock()).SearchAsync(
            new(null, null, null, null, int.MaxValue, 100));
        Assert.Empty(result.Items);
        Assert.Equal(1, result.TotalItems);
    }

    [PostgreSqlFact]
    public async Task CandidateCursorAdvancesAndReimportFindsExtraEdition()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await using var db = fixture.Open();
        var clock = new TestClock();
        await ImportAsync(db, clock, 2);
        var initial = await new EfAnalysisCandidateStore(db, clock).GetByDateAsync(new(2026, 10, 1), 1);
        var next = await new EfAnalysisCandidateStore(db, clock).GetByDateAsync(new(2026, 10, 1), 100,
            afterExternalId: initial[0].ExternalId);
        Assert.Single(next);
        Assert.NotEqual(initial[0].ExternalId, next[0].ExternalId);
        var imported = await ImportAsync(db, clock, 3);
        Assert.Equal(1, imported.Created);
        Assert.Equal(3, await db.SourceDocuments.CountAsync());
    }

    [PostgreSqlFact]
    public async Task ExpiredFifthAttemptBecomesTerminalAndDoesNotBlockNextMessage()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await using var db = fixture.Open();
        var clock = new TestClock();
        var message = OutboxMessage.Create("digest", "terminal", "test@example.invalid", "Test", "Test", null, clock.Now);
        for (var index = 0; index < 5; index++)
        {
            message.Claim(clock.Now);
            if (index < 4) message.MarkFailed(clock.Now, "fixture");
        }
        db.OutboxMessages.Add(message);
        db.OutboxMessages.Add(OutboxMessage.Create("digest", "next", "test@example.invalid", "Test", "Test", null, clock.Now.AddSeconds(1)));
        await db.SaveChangesAsync();
        clock.Now = clock.Now.AddMinutes(11);
        var claimed = await new EfSubscriptionStore(db, clock).ClaimMessagesAsync(1, clock.Now, default);
        Assert.Equal("next", Assert.Single(claimed).IdempotencyKey);
        var terminal = await db.OutboxMessages.AsNoTracking().SingleAsync(item => item.Id == message.Id);
        Assert.Equal(DeliveryStatus.Failed, terminal.Status);
        Assert.Equal(5, terminal.AttemptCount);
        Assert.Equal("claim-expired-final-delivery-unknown", terminal.LastError);
    }

    [PostgreSqlFact]
    public async Task OldWorkerCannotCompleteFailOrCancelReclaimedMessage()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await using var db = fixture.Open();
        var clock = new TestClock();
        db.OutboxMessages.Add(OutboxMessage.Create("digest", "leased", "test@example.invalid", "Test", "Test", null, clock.Now));
        await db.SaveChangesAsync();
        var store = new EfSubscriptionStore(db, clock);
        var old = Assert.Single(await store.ClaimMessagesAsync(1, clock.Now, default));
        clock.Now = clock.Now.AddMinutes(11);
        await using var secondDb = fixture.Open();
        var currentStore = new EfSubscriptionStore(secondDb, clock);
        var current = Assert.Single(await currentStore.ClaimMessagesAsync(1, clock.Now, default));
        Assert.Equal(2, current.AttemptCount);
        Assert.False(await store.MarkSentAsync(old.Id, old.AttemptCount, clock.Now, default));
        await store.MarkFailedAsync(old.Id, old.AttemptCount, "stale", clock.Now, default);
        await store.CancelAsync(old.Id, old.AttemptCount, default);
        var unchanged = await db.OutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(DeliveryStatus.Sending, unchanged.Status);
        Assert.Equal(2, unchanged.AttemptCount);
        Assert.True(await currentStore.MarkSentAsync(current.Id, current.AttemptCount, clock.Now, default));
        Assert.Equal(DeliveryStatus.Sent, (await db.OutboxMessages.AsNoTracking().SingleAsync()).Status);
    }

    private static Task<ImportIssueResult> ImportAsync(BoeRadarDbContext db, TestClock clock, int count) =>
        new EfIngestionStore(db, clock).ImportAsync(new("BOE", new(2026, 10, 1), new Uri("https://www.boe.es/"),
            Enumerable.Range(1, count).Select(index => new OfficialDocument($"BOE-A-2026-{index:00000}",
                index == 3 ? "extra" : "normal", "1", "Disposiciones generales", "1", "Ministerio", null,
                "Ayudas para pymes", null, null, new Uri($"https://www.boe.es/{index}.xml"), null)).ToArray()), "manual");

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 4, 10, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class QueryCounter : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class RefreshSource : IOfficialGazetteSource
    {
        public string SourceCode => "BOE";
        public DateOnly? FailDate { get; set; }
        private readonly Dictionary<DateOnly, int> calls = [];
        public Task<OfficialIssue> GetIssueAsync(DateOnly date, CancellationToken cancellationToken = default)
        {
            if (date == FailDate) throw new HttpRequestException("Temporary fixture source failure");
            calls.TryGetValue(date, out var count);
            calls[date] = ++count;
            return Task.FromResult(new OfficialIssue("BOE", date, new Uri("https://www.boe.es/"),
                Enumerable.Range(1, Math.Min(count, 2)).Select(index => new OfficialDocument(
                    $"BOE-A-2026-{date.DayNumber}{index}", "1", "1", "General", "1", "Ministerio", null,
                    "Ayudas", null, null, null, null)).ToArray()));
        }
    }

    private sealed class TestDatabase(string connection, string schema) : IAsyncDisposable
    {
        public BoeRadarDbContext Open(DbCommandInterceptor? interceptor = null)
        {
            var options = new DbContextOptionsBuilder<BoeRadarDbContext>()
                .UseNpgsql(connection, provider => provider.MigrationsHistoryTable("__EFMigrationsHistory", schema));
            if (interceptor is not null) options.AddInterceptors(interceptor);
            return new(options.Options);
        }

        public static async Task<TestDatabase> CreateAsync(string? targetMigration = null)
        {
            var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("BOERADAR_TEST_CONNECTION"));
            if (settings.Host is not ("localhost" or "127.0.0.1") || settings.Port != 54329)
                throw new InvalidOperationException("Integration tests only accept local PostgreSQL on port 54329.");
            var schema = "boeradar_test_" + Guid.NewGuid().ToString("N");
            await using (var admin = new NpgsqlConnection(settings.ConnectionString))
            {
                await admin.OpenAsync();
                await using var command = new NpgsqlCommand($"CREATE SCHEMA {schema}", admin);
                await command.ExecuteNonQueryAsync();
            }
            settings.SearchPath = schema;
            var fixture = new TestDatabase(settings.ConnectionString, schema);
            try
            {
                await using var db = fixture.Open();
                await db.GetService<IMigrator>().MigrateAsync(targetMigration);
                return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }

        public async ValueTask DisposeAsync()
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(schema, "^boeradar_test_[a-f0-9]{32}$"))
                throw new InvalidOperationException("Unsafe test schema.");
            await using var connectionToTest = new NpgsqlConnection(connection);
            await connectionToTest.OpenAsync();
            await using var cleanup = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", connectionToTest);
            await cleanup.ExecuteNonQueryAsync();
        }
    }
}
