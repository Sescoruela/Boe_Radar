namespace BoeRadar.Domain;

public enum SubscriptionStatus { Pending, Active, Unsubscribed, Bounced }
public enum DeliveryStatus { Pending, Sending, Sent, Failed, Canceled }

public sealed class Subscription
{
    private Subscription() { }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public SubscriptionStatus Status { get; private set; }
    public string VerificationTokenHash { get; private set; } = string.Empty;
    public DateTimeOffset VerificationExpiresAt { get; private set; }
    public string ManagementTokenHash { get; private set; } = string.Empty;
    public string CategoriesJson { get; private set; } = "[]";
    public string KeywordsJson { get; private set; } = "[]";
    public string Timezone { get; private set; } = "Europe/Madrid";
    public int DigestHour { get; private set; } = 8;
    public DateTimeOffset ConsentedAt { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public DateTimeOffset? UnsubscribedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Subscription Create(string email, string verificationHash, DateTimeOffset expiresAt,
        string categoriesJson, string keywordsJson, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(), Email = email, Status = SubscriptionStatus.Pending,
        VerificationTokenHash = verificationHash, VerificationExpiresAt = expiresAt,
        CategoriesJson = categoriesJson, KeywordsJson = keywordsJson,
        ConsentedAt = now, CreatedAt = now, UpdatedAt = now
    };

    public void RenewVerification(string hash, DateTimeOffset expiresAt,
        string categoriesJson, string keywordsJson, DateTimeOffset now)
    {
        Status = SubscriptionStatus.Pending;
        VerificationTokenHash = hash;
        VerificationExpiresAt = expiresAt;
        ManagementTokenHash = string.Empty;
        CategoriesJson = categoriesJson;
        KeywordsJson = keywordsJson;
        DigestHour = 8;
        ConsentedAt = now;
        UnsubscribedAt = null;
        UpdatedAt = now;
    }

    public void Activate(string managementHash, DateTimeOffset now)
    {
        if (Status != SubscriptionStatus.Pending || VerificationExpiresAt <= now)
            throw new InvalidOperationException("La verificación no está disponible.");
        Status = SubscriptionStatus.Active;
        VerificationTokenHash = string.Empty;
        ManagementTokenHash = managementHash;
        VerifiedAt = now;
        UpdatedAt = now;
    }

    public void UpdatePreferences(string categoriesJson, string keywordsJson, int digestHour, DateTimeOffset now)
    {
        if (Status != SubscriptionStatus.Active) throw new InvalidOperationException("La suscripción no está activa.");
        if (digestHour is < 0 or > 23) throw new ArgumentOutOfRangeException(nameof(digestHour));
        CategoriesJson = categoriesJson;
        KeywordsJson = keywordsJson;
        DigestHour = digestHour;
        UpdatedAt = now;
    }

    public void UpdateDigestHour(int digestHour)
    {
        if (digestHour is < 0 or > 23) throw new ArgumentOutOfRangeException(nameof(digestHour));
        DigestHour = digestHour;
    }

    public void Unsubscribe(DateTimeOffset now)
    {
        Status = SubscriptionStatus.Unsubscribed;
        VerificationTokenHash = string.Empty;
        ManagementTokenHash = string.Empty;
        UnsubscribedAt = now;
        UpdatedAt = now;
    }

    public void MarkBounced(DateTimeOffset now)
    {
        Status = SubscriptionStatus.Bounced;
        UpdatedAt = now;
    }
}

public sealed class AlertDigest
{
    private AlertDigest() { }

    public Guid Id { get; private set; }
    public Guid SubscriptionId { get; private set; }
    public DateOnly DigestDate { get; private set; }
    public int ItemCount { get; private set; }
    public string UnsubscribeTokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }

    public static AlertDigest Create(Guid subscriptionId, DateOnly date, int count,
        string unsubscribeHash, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(), SubscriptionId = subscriptionId, DigestDate = date,
        ItemCount = count, UnsubscribeTokenHash = unsubscribeHash, CreatedAt = now
    };

    public void MarkSent(DateTimeOffset now) => SentAt = now;
    public void ConsumeUnsubscribeToken() => UnsubscribeTokenHash = string.Empty;
}

public sealed class AlertMatch
{
    private AlertMatch() { }
    public Guid Id { get; private set; }
    public Guid DigestId { get; private set; }
    public Guid SubscriptionId { get; private set; }
    public Guid AnalysisId { get; private set; }
    public string ReasonsJson { get; private set; } = "[]";
    public DateTimeOffset MatchedAt { get; private set; }

    public static AlertMatch Create(Guid digestId, Guid subscriptionId, Guid analysisId,
        string reasonsJson, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(), DigestId = digestId, SubscriptionId = subscriptionId,
        AnalysisId = analysisId, ReasonsJson = reasonsJson, MatchedAt = now
    };
}

public sealed class OutboxMessage
{
    private OutboxMessage() { }
    public Guid Id { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string Recipient { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public Guid? DigestId { get; private set; }
    public DeliveryStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public string? LastError { get; private set; }

    public static OutboxMessage Create(string kind, string key, string recipient,
        string subject, string body, Guid? digestId, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(), Kind = kind, IdempotencyKey = key,
        Recipient = recipient, Subject = subject, Body = body, DigestId = digestId,
        Status = DeliveryStatus.Pending, NextAttemptAt = now, CreatedAt = now
    };

    public void Claim(DateTimeOffset now)
    {
        if (Status is not (DeliveryStatus.Pending or DeliveryStatus.Failed))
            throw new InvalidOperationException("El mensaje no puede reservarse.");
        Status = DeliveryStatus.Sending;
        AttemptCount++;
        NextAttemptAt = now.AddMinutes(10);
    }

    public void MarkSent(DateTimeOffset now)
    {
        Status = DeliveryStatus.Sent;
        SentAt = now;
        LastError = null;
        Body = string.Empty;
    }

    public void MarkFailed(DateTimeOffset now, string error)
    {
        Status = DeliveryStatus.Failed;
        LastError = error[..Math.Min(error.Length, 200)];
        NextAttemptAt = now.AddMinutes(Math.Min(60, 1 << Math.Min(AttemptCount, 6)));
    }

    public void Cancel()
    {
        Status = DeliveryStatus.Canceled;
        Body = string.Empty;
        LastError = null;
    }
}
