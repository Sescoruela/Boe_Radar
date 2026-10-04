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

namespace BoeRadar.UnitTests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BOERADAR_TEST_CONNECTION")))
            Skip = "Set BOERADAR_TEST_CONNECTION to the local test PostgreSQL (localhost:54329).";
    }
}

// Each test migrates an isolated schema. It never changes the application's public schema.
public sealed class PersistenceRegressionTests
{
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
        db.SourceReviews.Add(new StoredSourceReview { ExternalId = "BOE-A-2026-1", SourceHash = original.SourceHash,
            Version = "source-review-v1", RecordedAt = clock.Now,
            ReviewJson = JsonSerializer.Serialize(original, new JsonSerializerOptions(JsonSerializerDefaults.Web)) });
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
