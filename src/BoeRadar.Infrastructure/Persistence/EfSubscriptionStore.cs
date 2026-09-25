using System.Text;
using System.Text.Json;
using BoeRadar.Application;
using BoeRadar.Domain;
using Microsoft.EntityFrameworkCore;

namespace BoeRadar.Infrastructure.Persistence;

internal sealed class EfSubscriptionStore(BoeRadarDbContext db) : ISubscriptionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task RegisterAsync(string email, SubscriptionPreferences preferences,
        string tokenHash, string verificationUrl, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await db.Subscriptions.SingleOrDefaultAsync(item => item.Email == email, cancellationToken);
        if (existing?.Status == SubscriptionStatus.Active || existing?.Status == SubscriptionStatus.Bounced)
            return;
        if (existing?.Status == SubscriptionStatus.Pending && existing.UpdatedAt > now.AddHours(-1)) return;

        var body = $"Confirma tu suscripción a BOE Radar IA:\n{verificationUrl}\n\nEl enlace caduca en 24 horas. Si no solicitaste la suscripción, ignora este mensaje.";
        if (existing is null)
        {
            var subscription = Subscription.Create(email, tokenHash, now.AddHours(24),
                JsonSerializer.Serialize(preferences.Categories, JsonOptions),
                JsonSerializer.Serialize(preferences.Keywords, JsonOptions), now);
            subscription.UpdateDigestHour(preferences.DigestHour);
            db.Subscriptions.Add(subscription);
            db.OutboxMessages.Add(OutboxMessage.Create("verification", $"verify:{subscription.Id}:{tokenHash}",
                email, "Confirma tu suscripción a BOE Radar IA", body, null, now));
        }
        else
        {
            existing.RenewVerification(tokenHash, now.AddHours(24),
                JsonSerializer.Serialize(preferences.Categories, JsonOptions),
                JsonSerializer.Serialize(preferences.Keywords, JsonOptions), now);
            existing.UpdateDigestHour(preferences.DigestHour);
            db.OutboxMessages.Add(OutboxMessage.Create("verification", $"verify:{existing.Id}:{tokenHash}",
                email, "Confirma tu suscripción a BOE Radar IA", body, null, now));
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> VerifyAsync(string tokenHash, string managementHash,
        string managementUrl, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var subscription = await db.Subscriptions.SingleOrDefaultAsync(
            item => item.VerificationTokenHash == tokenHash && item.Status == SubscriptionStatus.Pending,
            cancellationToken);
        if (subscription is null || subscription.VerificationExpiresAt <= now) return null;
        subscription.Activate(managementHash, now);
        db.OutboxMessages.Add(OutboxMessage.Create("management", $"manage:{subscription.Id}:{managementHash}",
            subscription.Email, "Gestiona tus alertas de BOE Radar IA",
            $"Tu suscripción está activa. Guarda este enlace para cambiar preferencias o darte de baja:\n{managementUrl}",
            null, now));
        await db.SaveChangesAsync(cancellationToken);
        return subscription.Email;
    }

    public async Task<SubscriptionView?> GetAsync(string managementHash,
        CancellationToken cancellationToken)
    {
        var subscription = await db.Subscriptions.AsNoTracking().SingleOrDefaultAsync(
            item => item.ManagementTokenHash == managementHash && item.Status == SubscriptionStatus.Active,
            cancellationToken);
        return subscription is null ? null : ToView(subscription);
    }

    public async Task<bool> UpdateAsync(string managementHash, SubscriptionPreferences preferences,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var subscription = await db.Subscriptions.SingleOrDefaultAsync(
            item => item.ManagementTokenHash == managementHash && item.Status == SubscriptionStatus.Active,
            cancellationToken);
        if (subscription is null) return false;
        subscription.UpdatePreferences(JsonSerializer.Serialize(preferences.Categories, JsonOptions),
            JsonSerializer.Serialize(preferences.Keywords, JsonOptions), preferences.DigestHour, now);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UnsubscribeAsync(string tokenHash, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var subscription = await db.Subscriptions.SingleOrDefaultAsync(
            item => item.ManagementTokenHash == tokenHash && item.Status == SubscriptionStatus.Active,
            cancellationToken);
        if (subscription is null)
        {
            var digest = await db.AlertDigests.SingleOrDefaultAsync(
                item => item.UnsubscribeTokenHash == tokenHash && item.UnsubscribeTokenHash != "",
                cancellationToken);
            if (digest is null) return false;
            subscription = await db.Subscriptions.SingleOrDefaultAsync(
                item => item.Id == digest.SubscriptionId && item.Status == SubscriptionStatus.Active,
                cancellationToken);
            if (subscription is null) return false;
            digest.ConsumeUnsubscribeToken();
        }
        subscription.Unsubscribe(now);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<DigestPlan> QueueDigestsAsync(DateOnly date, Uri publicBaseUri,
        bool includeHeuristic, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var subscriptions = await db.Subscriptions.AsNoTracking()
            .Where(item => item.Status == SubscriptionStatus.Active)
            .ToArrayAsync(cancellationToken);
        var analyzed = await db.DocumentAnalyses.AsNoTracking()
            .Join(db.SourceDocuments.AsNoTracking().Where(document => document.PublicationDate == date),
                analysis => analysis.DocumentId, document => document.Id,
                (analysis, document) => new { Analysis = analysis, Document = document })
            .Where(item => item.Analysis.IsRelevant &&
                (includeHeuristic || item.Analysis.Method == "gemini"))
            .ToArrayAsync(cancellationToken);
        var latest = analyzed.GroupBy(item => item.Document.Id)
            .Select(group => group.OrderByDescending(item => item.Analysis.AnalyzedAt).First())
            .ToArray();
        var queued = 0;
        var matches = 0;

        foreach (var subscription in subscriptions)
        {
            var madridNow = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid"));
            if (DateOnly.FromDateTime(madridNow.DateTime) == date && madridNow.Hour < subscription.DigestHour)
                continue;
            if (await db.AlertDigests.AnyAsync(item => item.SubscriptionId == subscription.Id &&
                item.DigestDate == date, cancellationToken)) continue;
            var alreadyMatchedDocumentIds = await db.AlertMatches.AsNoTracking()
                .Where(item => item.SubscriptionId == subscription.Id)
                .Join(db.DocumentAnalyses.AsNoTracking(), item => item.AnalysisId,
                    analysis => analysis.Id, (item, analysis) => analysis.DocumentId)
                .ToArrayAsync(cancellationToken);
            var seen = alreadyMatchedDocumentIds.ToHashSet();
            var preferences = ToView(subscription).Preferences;
            var selected = latest
                .Where(item => !seen.Contains(item.Document.Id))
                .Select(item => new
                {
                    item.Analysis,
                    Item = new DigestItem(item.Analysis.Id, item.Document.Id,
                        item.Document.ExternalId, item.Document.Title, item.Analysis.Summary,
                        item.Analysis.Category, item.Analysis.Method, item.Document.OfficialPdfUrl,
                        JsonSerializer.Deserialize<string[]>(item.Analysis.RequirementsJson, JsonOptions),
                        JsonSerializer.Deserialize<RadarDeadline[]>(item.Analysis.DeadlinesJson, JsonOptions))
                })
                .Select(item => new { item.Analysis, item.Item,
                    Reasons = SubscriptionRules.Match(preferences, item.Item) })
                .Where(item => item.Reasons.Count > 0)
                .ToArray();
            if (selected.Length == 0) continue;

            var unsubscribeToken = SubscriptionTokens.Generate();
            var digest = AlertDigest.Create(subscription.Id, date, selected.Length,
                SubscriptionTokens.Hash(unsubscribeToken), now);
            var body = BuildDigestBody(date, selected.Select(item => item.Item).ToArray(),
                publicBaseUri, unsubscribeToken);
            db.AlertDigests.Add(digest);
            foreach (var item in selected)
            {
                db.AlertMatches.Add(AlertMatch.Create(digest.Id, subscription.Id,
                    item.Analysis.Id, JsonSerializer.Serialize(item.Reasons, JsonOptions), now));
            }
            db.OutboxMessages.Add(OutboxMessage.Create("digest", $"digest:{subscription.Id}:{date:yyyyMMdd}",
                subscription.Email, $"BOE Radar IA · {date:dd/MM/yyyy} · {selected.Length} novedades",
                body, digest.Id, now));
            await db.SaveChangesAsync(cancellationToken);
            queued++;
            matches += selected.Length;
        }
        return new DigestPlan(subscriptions.Length, queued, matches);
    }

    public async Task<IReadOnlyList<OutboxMessage>> ClaimMessagesAsync(int limit,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var messages = await db.OutboxMessages.FromSqlInterpolated($"""
            SELECT * FROM outbox_messages
            WHERE ((status IN ('Pending', 'Failed') AND next_attempt_at <= {now})
                OR (status = 'Sending' AND next_attempt_at <= {now}))
                AND attempt_count < 5
            ORDER BY created_at
            LIMIT {limit}
            FOR UPDATE SKIP LOCKED
            """).ToArrayAsync(cancellationToken);
        foreach (var message in messages)
        {
            if (message.Status == DeliveryStatus.Sending)
                message.MarkFailed(now, "claim-expired");
            message.Claim(now);
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return messages;
    }

    public async Task MarkSentAsync(Guid messageId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var message = await db.OutboxMessages.SingleAsync(item => item.Id == messageId, cancellationToken);
        message.MarkSent(now);
        if (message.DigestId is { } digestId)
        {
            var digest = await db.AlertDigests.SingleAsync(item => item.Id == digestId, cancellationToken);
            digest.MarkSent(now);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(Guid messageId, string error, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var message = await db.OutboxMessages.SingleAsync(item => item.Id == messageId, cancellationToken);
        message.MarkFailed(now, error);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> CanDeliverAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var message = await db.OutboxMessages.AsNoTracking().SingleAsync(item => item.Id == messageId,
            cancellationToken);
        if (message.Status != DeliveryStatus.Sending) return false;
        if (message.Kind == "management")
        {
            var managementHash = message.IdempotencyKey.Split(':').Last();
            return await db.Subscriptions.AsNoTracking().AnyAsync(item => item.Email == message.Recipient &&
                item.Status == SubscriptionStatus.Active && item.ManagementTokenHash == managementHash,
                cancellationToken);
        }
        if (message.Kind == "verification")
        {
            var tokenHash = message.IdempotencyKey.Split(':').Last();
            return await db.Subscriptions.AsNoTracking().AnyAsync(item => item.Email == message.Recipient &&
                item.Status == SubscriptionStatus.Pending && item.VerificationTokenHash == tokenHash &&
                item.VerificationExpiresAt > DateTimeOffset.UtcNow,
                cancellationToken);
        }
        return await db.Subscriptions.AsNoTracking().AnyAsync(item => item.Email == message.Recipient &&
            item.Status == SubscriptionStatus.Active, cancellationToken);
    }

    public async Task CancelAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var message = await db.OutboxMessages.SingleAsync(item => item.Id == messageId, cancellationToken);
        message.Cancel();
        await db.SaveChangesAsync(cancellationToken);
    }

    private static SubscriptionView ToView(Subscription subscription) => new(
        subscription.Email, subscription.Status.ToString(),
        new SubscriptionPreferences(
            JsonSerializer.Deserialize<RadarCategory[]>(subscription.CategoriesJson, JsonOptions) ?? [],
            JsonSerializer.Deserialize<string[]>(subscription.KeywordsJson, JsonOptions) ?? [],
            subscription.DigestHour));

    private static string BuildDigestBody(DateOnly date, IReadOnlyList<DigestItem> items,
        Uri publicBaseUri, string unsubscribeToken)
    {
        var body = new StringBuilder($"BOE Radar IA · {date:dd/MM/yyyy}\n\n");
        foreach (var item in items)
        {
            body.AppendLine($"• {item.Title}");
            body.AppendLine($"  {item.Category}: {item.Summary}");
            if (item.Requirements is { Count: > 0 })
                body.AppendLine($"  Requisitos: {string.Join("; ", item.Requirements)}");
            if (item.Deadlines is { Count: > 0 })
            {
                foreach (var deadline in item.Deadlines)
                    body.AppendLine($"  Plazo: {deadline.Date ?? "sin fecha explícita"} · {deadline.Description}");
            }
            body.AppendLine($"  Fuente: {item.OfficialPdfUrl ?? $"https://www.boe.es/diario_boe/txt.php?id={item.ExternalId}"}");
            body.AppendLine($"  Método: {item.Method}");
            body.AppendLine();
        }
        body.AppendLine("Información orientativa. Comprueba siempre la publicación oficial.");
        body.AppendLine($"Baja de un solo uso: {new Uri(publicBaseUri, $"/#unsubscribe={unsubscribeToken}")}");
        return body.ToString();
    }
}
