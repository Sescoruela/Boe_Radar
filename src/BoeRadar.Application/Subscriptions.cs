using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using BoeRadar.Domain;

namespace BoeRadar.Application;

public sealed record SubscriptionPreferences(
    IReadOnlyList<RadarCategory> Categories,
    IReadOnlyList<string> Keywords,
    int DigestHour = 8);

public sealed record SubscriptionView(
    string Email,
    string Status,
    SubscriptionPreferences Preferences);

public sealed record DigestItem(Guid AnalysisId, Guid DocumentId, string ExternalId,
    string Title, string Summary, RadarCategory Category, string Method,
    string? OfficialPdfUrl, IReadOnlyList<string>? Requirements = null,
    IReadOnlyList<RadarDeadline>? Deadlines = null);

public sealed record DigestPlan(int Subscribers, int DigestsQueued, int Matches);
public sealed record DispatchResult(int Sent, int Failed);

public interface ISubscriptionStore
{
    Task RegisterAsync(string email, SubscriptionPreferences preferences, string tokenHash,
        string verificationUrl, DateTimeOffset now, CancellationToken cancellationToken);
    Task<string?> VerifyAsync(string tokenHash, string managementHash, string managementUrl, DateTimeOffset now,
        CancellationToken cancellationToken);
    Task<SubscriptionView?> GetAsync(string managementHash, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(string managementHash, SubscriptionPreferences preferences,
        DateTimeOffset now, CancellationToken cancellationToken);
    Task<bool> UnsubscribeAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken);
    Task<DigestPlan> QueueDigestsAsync(DateOnly date, Uri publicBaseUri,
        bool includeHeuristic, DateTimeOffset now, CancellationToken cancellationToken);
    Task<IReadOnlyList<OutboxMessage>> ClaimMessagesAsync(int limit, DateTimeOffset now,
        CancellationToken cancellationToken);
    Task MarkSentAsync(Guid messageId, DateTimeOffset now, CancellationToken cancellationToken);
    Task MarkFailedAsync(Guid messageId, string error, DateTimeOffset now,
        CancellationToken cancellationToken);
    Task<bool> CanDeliverAsync(Guid messageId, CancellationToken cancellationToken);
    Task CancelAsync(Guid messageId, CancellationToken cancellationToken);
}

public interface IEmailSender
{
    Task SendAsync(string recipient, string subject, string body,
        CancellationToken cancellationToken);
}

public static class SubscriptionTokens
{
    public static string Generate() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public sealed class SubscriptionRules
{
    public static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254)
            throw new ArgumentException("Introduce un correo válido.", nameof(email));
        var normalized = email.Trim().ToLowerInvariant();
        try
        {
            if (new MailAddress(normalized).Address != normalized)
                throw new FormatException();
        }
        catch (FormatException) { throw new ArgumentException("Introduce un correo válido.", nameof(email)); }
        return normalized;
    }

    public static SubscriptionPreferences Normalize(SubscriptionPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (preferences.DigestHour is < 0 or > 23)
            throw new ArgumentException("La hora del digest debe estar entre 0 y 23.");
        if (preferences.Categories is null || preferences.Keywords is null ||
            preferences.Keywords.Any(keyword => keyword is null))
            throw new ArgumentException("Las preferencias no son válidas.");
        var categories = preferences.Categories.Distinct().ToArray();
        if (categories.Length > 7 || categories.Any(category => !Enum.IsDefined(category)))
            throw new ArgumentException("Las categorías no son válidas.");
        var keywords = preferences.Keywords
            .Select(keyword => keyword.Trim().ToLowerInvariant())
            .Where(keyword => keyword.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (keywords.Length > 10 || keywords.Any(keyword => keyword.Length > 80))
            throw new ArgumentException("Se admiten hasta 10 palabras clave de 80 caracteres.");
        return new SubscriptionPreferences(categories, keywords, preferences.DigestHour);
    }

    public static IReadOnlyList<string> Match(SubscriptionPreferences preferences, DigestItem item)
    {
        var reasons = new List<string>();
        if (preferences.Categories.Count == 0 || preferences.Categories.Contains(item.Category))
            reasons.Add($"category:{item.Category}");
        foreach (var keyword in preferences.Keywords)
        {
            if (item.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                item.Summary.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                reasons.Add($"keyword:{keyword}");
        }
        if (preferences.Categories.Count > 0 && !preferences.Categories.Contains(item.Category))
            return [];
        if (preferences.Keywords.Count > 0 && reasons.All(reason => !reason.StartsWith("keyword:", StringComparison.Ordinal)))
            return [];
        return reasons;
    }
}

public sealed class SubscriptionService(ISubscriptionStore store, TimeProvider clock)
{
    public async Task RegisterAsync(string email, SubscriptionPreferences preferences,
        Uri publicBaseUri, CancellationToken cancellationToken)
    {
        var normalized = SubscriptionRules.NormalizeEmail(email);
        var clean = SubscriptionRules.Normalize(preferences);
        var token = SubscriptionTokens.Generate();
        var url = new Uri(publicBaseUri, $"/#verify={token}").ToString();
        await store.RegisterAsync(normalized, clean, SubscriptionTokens.Hash(token),
            url, clock.GetUtcNow(), cancellationToken);
    }

    public async Task<string?> VerifyAsync(string token, Uri publicBaseUri, CancellationToken cancellationToken)
    {
        if (!IsToken(token)) return null;
        var managementToken = SubscriptionTokens.Generate();
        var email = await store.VerifyAsync(SubscriptionTokens.Hash(token),
            SubscriptionTokens.Hash(managementToken),
            new Uri(publicBaseUri, $"/#manage={managementToken}").ToString(),
            clock.GetUtcNow(), cancellationToken);
        return email is null ? null : managementToken;
    }

    public Task<SubscriptionView?> GetAsync(string token, CancellationToken cancellationToken) =>
        IsToken(token)
            ? store.GetAsync(SubscriptionTokens.Hash(token), cancellationToken)
            : Task.FromResult<SubscriptionView?>(null);

    public Task<bool> UpdateAsync(string token, SubscriptionPreferences preferences,
        CancellationToken cancellationToken) =>
        IsToken(token)
            ? store.UpdateAsync(SubscriptionTokens.Hash(token),
                SubscriptionRules.Normalize(preferences), clock.GetUtcNow(), cancellationToken)
            : Task.FromResult(false);

    public Task<bool> UnsubscribeAsync(string token, CancellationToken cancellationToken) =>
        IsToken(token)
            ? store.UnsubscribeAsync(SubscriptionTokens.Hash(token), clock.GetUtcNow(), cancellationToken)
            : Task.FromResult(false);

    private static bool IsToken(string? token) => token?.Length == 64 &&
        token.All(character => Uri.IsHexDigit(character));
}

public sealed class DigestService(ISubscriptionStore store, IEmailSender emailSender, TimeProvider clock)
{
    public Task<DigestPlan> QueueAsync(DateOnly date, Uri publicBaseUri,
        bool includeHeuristic, CancellationToken cancellationToken) =>
        store.QueueDigestsAsync(date, publicBaseUri, includeHeuristic,
            clock.GetUtcNow(), cancellationToken);

    public async Task<DispatchResult> DispatchAsync(int limit, CancellationToken cancellationToken)
    {
        var messages = await store.ClaimMessagesAsync(Math.Clamp(limit, 1, 100),
            clock.GetUtcNow(), cancellationToken);
        var sent = 0;
        var failed = 0;
        foreach (var message in messages)
        {
            try
            {
                if (!await store.CanDeliverAsync(message.Id, cancellationToken))
                {
                    await store.CancelAsync(message.Id, cancellationToken);
                    continue;
                }
                await emailSender.SendAsync(message.Recipient, message.Subject,
                    message.Body, cancellationToken);
                await store.MarkSentAsync(message.Id, clock.GetUtcNow(), cancellationToken);
                sent++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await store.MarkFailedAsync(message.Id, exception.GetType().Name,
                    clock.GetUtcNow(), cancellationToken);
                failed++;
            }
        }
        return new DispatchResult(sent, failed);
    }
}
