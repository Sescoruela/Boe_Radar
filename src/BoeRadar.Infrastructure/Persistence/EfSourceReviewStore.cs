using System.Text.Json;
using BoeRadar.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoeRadar.Infrastructure.Persistence;

public sealed class StoredSourceReview
{
    public string ExternalId { get; set; } = "";
    public string SourceHash { get; set; } = "";
    public string Version { get; set; } = "";
    public DateTimeOffset RecordedAt { get; set; }
    public string ReviewJson { get; set; } = "";
}

public sealed class StoredSourceReviewConfiguration : IEntityTypeConfiguration<StoredSourceReview>
{
    public void Configure(EntityTypeBuilder<StoredSourceReview> builder)
    {
        builder.ToTable("source_reviews");
        builder.HasKey(item => new { item.ExternalId, item.SourceHash, item.Version });
        builder.Property(item => item.ExternalId).HasColumnName("external_id").HasMaxLength(30);
        builder.Property(item => item.SourceHash).HasColumnName("source_hash").HasMaxLength(64);
        builder.Property(item => item.Version).HasColumnName("version").HasMaxLength(40);
        builder.Property(item => item.RecordedAt).HasColumnName("recorded_at");
        builder.Property(item => item.ReviewJson).HasColumnName("review_json").HasColumnType("jsonb");
        builder.HasIndex(item => new { item.Version, item.RecordedAt });
    }
}

public sealed class SourceReviewObservation
{
    public long Id { get; set; }
    public string ExternalId { get; set; } = "";
    public string SourceHash { get; set; } = "";
    public string Version { get; set; } = "";
    public DateTimeOffset ObservedAt { get; set; }
}

public sealed class SourceReviewObservationConfiguration : IEntityTypeConfiguration<SourceReviewObservation>
{
    public void Configure(EntityTypeBuilder<SourceReviewObservation> builder)
    {
        builder.ToTable("source_review_observations");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.ExternalId).HasColumnName("external_id").HasMaxLength(30);
        builder.Property(item => item.SourceHash).HasColumnName("source_hash").HasMaxLength(64);
        builder.Property(item => item.Version).HasColumnName("version").HasMaxLength(40);
        builder.Property(item => item.ObservedAt).HasColumnName("observed_at");
        builder.HasIndex(item => new { item.ExternalId, item.ObservedAt, item.Id });
        builder.HasOne<StoredSourceReview>().WithMany()
            .HasForeignKey(item => new { item.ExternalId, item.SourceHash, item.Version });
    }
}

internal sealed class EfSourceReviewStore(BoeRadarDbContext database, TimeProvider clock) : ISourceReviewStore
{
    private const string Version = "source-review-v2";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public async Task SaveAsync(string externalId, ActionableSourceReview review, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(review with { ProfileContrast = null }, JsonOptions);
        // The canonical body hash does not cover BOE's independently updated reference metadata.
        // Keep it in ReviewJson, but include references in the storage identity to retain changes.
        var storageHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(review.SourceHash + "\n" + JsonSerializer.Serialize(review.References, JsonOptions))));
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // Serialize observations of the same BOE document across web instances.
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({externalId}, 0))", cancellationToken);
        var recordedAt = clock.GetUtcNow().ToUniversalTime();
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO source_reviews (external_id, source_hash, version, recorded_at, review_json)
            VALUES ({externalId}, {storageHash}, {Version}, {recordedAt}, CAST({json} AS jsonb))
            ON CONFLICT (external_id, source_hash, version) DO NOTHING
            """, cancellationToken);
        var latest = await database.SourceReviewObservations.AsNoTracking()
            .Where(item => item.ExternalId == externalId).OrderByDescending(item => item.ObservedAt)
            .ThenByDescending(item => item.Id).FirstOrDefaultAsync(cancellationToken);
        if (latest is null || latest.SourceHash != storageHash || latest.Version != Version)
        {
            await database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO source_review_observations (external_id, source_hash, version, observed_at)
                VALUES ({externalId}, {storageHash}, {Version}, {recordedAt})
                """, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, ActionableSourceReview>> GetAsync(IReadOnlyList<string> externalIds,
        DateTimeOffset asOf, CancellationToken cancellationToken = default)
    {
        asOf = asOf.ToUniversalTime();
        var stored = await database.SourceReviewObservations.AsNoTracking()
            .Where(item => externalIds.Contains(item.ExternalId) &&
                (item.Version == Version || item.Version == "source-review-v1") && item.ObservedAt <= asOf)
            .Join(database.SourceReviews.AsNoTracking(),
                observation => new { observation.ExternalId, observation.SourceHash, observation.Version },
                review => new { review.ExternalId, review.SourceHash, review.Version },
                (observation, review) => new
                {
                    observation.ExternalId,
                    observation.ObservedAt,
                    observation.Id,
                    review.ReviewJson
                })
            .ToListAsync(cancellationToken);
        return stored.GroupBy(item => item.ExternalId).ToDictionary(group => group.Key, group =>
        {
            var latest = group.OrderByDescending(item => item.ObservedAt).ThenByDescending(item => item.Id).First();
            return JsonSerializer.Deserialize<ActionableSourceReview>(latest.ReviewJson, JsonOptions)!
                with
            { ReviewedAt = latest.ObservedAt };
        });
    }
}
