using BoeRadar.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoeRadar.Infrastructure.Persistence;

internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.Email).HasColumnName("email").HasMaxLength(254);
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        builder.Property(item => item.VerificationTokenHash).HasColumnName("verification_token_hash").HasMaxLength(64);
        builder.Property(item => item.VerificationExpiresAt).HasColumnName("verification_expires_at");
        builder.Property(item => item.ManagementTokenHash).HasColumnName("management_token_hash").HasMaxLength(64);
        builder.Property(item => item.CategoriesJson).HasColumnName("categories").HasColumnType("jsonb");
        builder.Property(item => item.KeywordsJson).HasColumnName("keywords").HasColumnType("jsonb");
        builder.Property(item => item.Timezone).HasColumnName("timezone").HasMaxLength(60);
        builder.Property(item => item.DigestHour).HasColumnName("digest_hour");
        builder.Property(item => item.ConsentedAt).HasColumnName("consented_at");
        builder.Property(item => item.VerifiedAt).HasColumnName("verified_at");
        builder.Property(item => item.UnsubscribedAt).HasColumnName("unsubscribed_at");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at");
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(item => item.Email).IsUnique();
        builder.HasIndex(item => item.VerificationTokenHash).IsUnique()
            .HasFilter("verification_token_hash <> ''");
        builder.HasIndex(item => item.ManagementTokenHash).IsUnique()
            .HasFilter("management_token_hash <> ''");
    }
}

internal sealed class AlertDigestConfiguration : IEntityTypeConfiguration<AlertDigest>
{
    public void Configure(EntityTypeBuilder<AlertDigest> builder)
    {
        builder.ToTable("alert_digests");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.SubscriptionId).HasColumnName("subscription_id");
        builder.Property(item => item.DigestDate).HasColumnName("digest_date");
        builder.Property(item => item.ItemCount).HasColumnName("item_count");
        builder.Property(item => item.UnsubscribeTokenHash).HasColumnName("unsubscribe_token_hash").HasMaxLength(64);
        builder.Property(item => item.CreatedAt).HasColumnName("created_at");
        builder.Property(item => item.SentAt).HasColumnName("sent_at");
        builder.HasIndex(item => new { item.SubscriptionId, item.DigestDate }).IsUnique();
        builder.HasIndex(item => item.UnsubscribeTokenHash).IsUnique()
            .HasFilter("unsubscribe_token_hash <> ''");
        builder.HasOne<Subscription>().WithMany().HasForeignKey(item => item.SubscriptionId);
    }
}

internal sealed class AlertMatchConfiguration : IEntityTypeConfiguration<AlertMatch>
{
    public void Configure(EntityTypeBuilder<AlertMatch> builder)
    {
        builder.ToTable("alert_matches");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.DigestId).HasColumnName("digest_id");
        builder.Property(item => item.SubscriptionId).HasColumnName("subscription_id");
        builder.Property(item => item.AnalysisId).HasColumnName("analysis_id");
        builder.Property(item => item.ReasonsJson).HasColumnName("reasons").HasColumnType("jsonb");
        builder.Property(item => item.MatchedAt).HasColumnName("matched_at");
        builder.HasIndex(item => new { item.SubscriptionId, item.AnalysisId }).IsUnique();
        builder.HasOne<AlertDigest>().WithMany().HasForeignKey(item => item.DigestId);
        builder.HasOne<Subscription>().WithMany().HasForeignKey(item => item.SubscriptionId);
        builder.HasOne<DocumentAnalysis>().WithMany().HasForeignKey(item => item.AnalysisId);
    }
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.Kind).HasColumnName("kind").HasMaxLength(40);
        builder.Property(item => item.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(200);
        builder.Property(item => item.Recipient).HasColumnName("recipient").HasMaxLength(254);
        builder.Property(item => item.Subject).HasColumnName("subject").HasMaxLength(200);
        builder.Property(item => item.Body).HasColumnName("body");
        builder.Property(item => item.DigestId).HasColumnName("digest_id");
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        builder.Property(item => item.AttemptCount).HasColumnName("attempt_count");
        builder.Property(item => item.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at");
        builder.Property(item => item.SentAt).HasColumnName("sent_at");
        builder.Property(item => item.LastError).HasColumnName("last_error").HasMaxLength(200);
        builder.HasIndex(item => item.IdempotencyKey).IsUnique();
        builder.HasIndex(item => new { item.Status, item.NextAttemptAt });
        builder.HasOne<AlertDigest>().WithMany().HasForeignKey(item => item.DigestId);
    }
}
