using BoeRadar.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoeRadar.Infrastructure.Persistence;

internal sealed class GazetteSourceConfiguration : IEntityTypeConfiguration<GazetteSource>
{
    public void Configure(EntityTypeBuilder<GazetteSource> builder)
    {
        builder.ToTable("gazette_sources");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id");
        builder.Property(entity => entity.Code).HasColumnName("code").HasMaxLength(32);
        builder.Property(entity => entity.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(entity => entity.BaseUrl).HasColumnName("base_url");
        builder.Property(entity => entity.IsActive).HasColumnName("is_active");
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(entity => entity.Code).IsUnique();
        builder.HasData(new
        {
            Id = GazetteSource.BoeId,
            Code = "BOE",
            Name = "Boletín Oficial del Estado",
            BaseUrl = "https://www.boe.es/",
            IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero)
        });
    }
}

internal sealed class PublicationIssueConfiguration : IEntityTypeConfiguration<PublicationIssue>
{
    public void Configure(EntityTypeBuilder<PublicationIssue> builder)
    {
        builder.ToTable("publication_issues");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id");
        builder.Property(entity => entity.SourceId).HasColumnName("source_id");
        builder.Property(entity => entity.ExternalId).HasColumnName("external_id").HasMaxLength(100);
        builder.Property(entity => entity.IssueNumber).HasColumnName("issue_number").HasMaxLength(30);
        builder.Property(entity => entity.PublicationDate).HasColumnName("publication_date");
        builder.Property(entity => entity.SourceUrl).HasColumnName("source_url");
        builder.Property(entity => entity.RawMetadata).HasColumnName("raw_metadata").HasColumnType("jsonb");
        builder.Property(entity => entity.DiscoveredAt).HasColumnName("discovered_at");
        builder.HasIndex(entity => new { entity.SourceId, entity.PublicationDate, entity.ExternalId })
            .IsUnique();
        builder.HasOne<GazetteSource>()
            .WithMany()
            .HasForeignKey(entity => entity.SourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SourceDocumentConfiguration : IEntityTypeConfiguration<SourceDocument>
{
    public void Configure(EntityTypeBuilder<SourceDocument> builder)
    {
        builder.ToTable("source_documents");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id");
        builder.Property(entity => entity.IssueId).HasColumnName("issue_id");
        builder.Property(entity => entity.SourceId).HasColumnName("source_id");
        builder.Property(entity => entity.ExternalId).HasColumnName("external_id").HasMaxLength(100);
        builder.Property(entity => entity.PublicationDate).HasColumnName("publication_date");
        builder.Property(entity => entity.Title).HasColumnName("title");
        builder.Property(entity => entity.DepartmentCode).HasColumnName("department_code").HasMaxLength(50);
        builder.Property(entity => entity.Department).HasColumnName("department").HasMaxLength(300);
        builder.Property(entity => entity.SectionCode).HasColumnName("section_code").HasMaxLength(30);
        builder.Property(entity => entity.SectionName).HasColumnName("section_name").HasMaxLength(200);
        builder.Property(entity => entity.Epigraph).HasColumnName("epigraph").HasMaxLength(300);
        builder.Property(entity => entity.ControlNumber).HasColumnName("control_number").HasMaxLength(100);
        builder.Property(entity => entity.OfficialHtmlUrl).HasColumnName("official_html_url");
        builder.Property(entity => entity.OfficialXmlUrl).HasColumnName("official_xml_url");
        builder.Property(entity => entity.OfficialPdfUrl).HasColumnName("official_pdf_url");
        builder.Property(entity => entity.RawMetadata).HasColumnName("raw_metadata").HasColumnType("jsonb");
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(entity => new { entity.SourceId, entity.ExternalId }).IsUnique();
        builder.HasIndex(entity => entity.PublicationDate);
        builder.HasIndex(entity => entity.SectionCode);
        builder.HasIndex(entity => entity.Department);
        builder.HasOne<PublicationIssue>()
            .WithMany()
            .HasForeignKey(entity => entity.IssueId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GazetteSource>()
            .WithMany()
            .HasForeignKey(entity => entity.SourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DocumentContentConfiguration : IEntityTypeConfiguration<DocumentContent>
{
    public void Configure(EntityTypeBuilder<DocumentContent> builder)
    {
        builder.ToTable("document_contents");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id");
        builder.Property(entity => entity.DocumentId).HasColumnName("document_id");
        builder.Property(entity => entity.ContentHash).HasColumnName("content_hash").HasMaxLength(64);
        builder.Property(entity => entity.Format).HasColumnName("format").HasMaxLength(20);
        builder.Property(entity => entity.NormalizedText).HasColumnName("normalized_text");
        builder.Property(entity => entity.StorageUri).HasColumnName("storage_uri");
        builder.Property(entity => entity.FetchedAt).HasColumnName("fetched_at");
        builder.Property(entity => entity.IsCurrent).HasColumnName("is_current");
        builder.HasIndex(entity => new { entity.DocumentId, entity.ContentHash }).IsUnique();
        builder.HasIndex(entity => entity.DocumentId)
            .IsUnique()
            .HasFilter("is_current = true");
        builder.HasOne<SourceDocument>()
            .WithMany()
            .HasForeignKey(entity => entity.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class IngestionRunConfiguration : IEntityTypeConfiguration<IngestionRun>
{
    public void Configure(EntityTypeBuilder<IngestionRun> builder)
    {
        builder.ToTable("ingestion_runs");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id");
        builder.Property(entity => entity.SourceId).HasColumnName("source_id");
        builder.Property(entity => entity.TargetDate).HasColumnName("target_date");
        builder.Property(entity => entity.Trigger).HasColumnName("trigger").HasMaxLength(30);
        builder.Property(entity => entity.Status)
            .HasColumnName("status")
            .HasMaxLength(30)
            .HasConversion<string>();
        builder.Property(entity => entity.StartedAt).HasColumnName("started_at");
        builder.Property(entity => entity.FinishedAt).HasColumnName("finished_at");
        builder.Property(entity => entity.Counters).HasColumnName("counters").HasColumnType("jsonb");
        builder.Property(entity => entity.ErrorSummary).HasColumnName("error_summary");
        builder.HasIndex(entity => new { entity.SourceId, entity.TargetDate });
        builder.HasOne<GazetteSource>()
            .WithMany()
            .HasForeignKey(entity => entity.SourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DocumentAnalysisConfiguration : IEntityTypeConfiguration<DocumentAnalysis>
{
    public void Configure(EntityTypeBuilder<DocumentAnalysis> builder)
    {
        builder.ToTable("document_analyses");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id");
        builder.Property(entity => entity.DocumentId).HasColumnName("document_id");
        builder.Property(entity => entity.ContentHash).HasColumnName("content_hash").HasMaxLength(64);
        builder.Property(entity => entity.IsRelevant).HasColumnName("is_relevant");
        builder.Property(entity => entity.Category).HasColumnName("category").HasMaxLength(30).HasConversion<string>();
        builder.Property(entity => entity.Summary).HasColumnName("summary");
        builder.Property(entity => entity.RequirementsJson).HasColumnName("requirements").HasColumnType("jsonb");
        builder.Property(entity => entity.DeadlinesJson).HasColumnName("deadlines").HasColumnType("jsonb");
        builder.Property(entity => entity.EvidenceJson).HasColumnName("evidence").HasColumnType("jsonb");
        builder.Property(entity => entity.Confidence).HasColumnName("confidence").HasPrecision(4, 3);
        builder.Property(entity => entity.Method).HasColumnName("method").HasMaxLength(30);
        builder.Property(entity => entity.ModelName).HasColumnName("model_name").HasMaxLength(100);
        builder.Property(entity => entity.PromptVersion).HasColumnName("prompt_version").HasMaxLength(50);
        builder.Property(entity => entity.InputTokens).HasColumnName("input_tokens");
        builder.Property(entity => entity.OutputTokens).HasColumnName("output_tokens");
        builder.Property(entity => entity.AnalyzedAt).HasColumnName("analyzed_at");
        builder.HasIndex(entity => new
        {
            entity.DocumentId,
            entity.ContentHash,
            entity.ModelName,
            entity.PromptVersion
        }).IsUnique();
        builder.HasIndex(entity => new { entity.IsRelevant, entity.Category });
        builder.HasOne<SourceDocument>()
            .WithMany()
            .HasForeignKey(entity => entity.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
