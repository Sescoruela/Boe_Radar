using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoeRadar.Infrastructure.Persistence;

public sealed class CatalogRefreshProgress
{
    public string Source { get; set; } = "BOE";
    public DateOnly NextDate { get; set; }
}

public sealed class CatalogRefreshProgressConfiguration : IEntityTypeConfiguration<CatalogRefreshProgress>
{
    public void Configure(EntityTypeBuilder<CatalogRefreshProgress> builder)
    {
        builder.ToTable("catalog_refresh_progress");
        builder.HasKey(item => item.Source);
        builder.Property(item => item.Source).HasColumnName("source").HasMaxLength(20);
        builder.Property(item => item.NextDate).HasColumnName("next_date");
    }
}
