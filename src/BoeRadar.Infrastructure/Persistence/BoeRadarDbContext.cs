using BoeRadar.Domain;
using Microsoft.EntityFrameworkCore;

namespace BoeRadar.Infrastructure.Persistence;

public sealed class BoeRadarDbContext(DbContextOptions<BoeRadarDbContext> options)
    : DbContext(options)
{
    public DbSet<GazetteSource> GazetteSources => Set<GazetteSource>();

    public DbSet<PublicationIssue> PublicationIssues => Set<PublicationIssue>();

    public DbSet<SourceDocument> SourceDocuments => Set<SourceDocument>();

    public DbSet<DocumentContent> DocumentContents => Set<DocumentContent>();

    public DbSet<IngestionRun> IngestionRuns => Set<IngestionRun>();

    public DbSet<DocumentAnalysis> DocumentAnalyses => Set<DocumentAnalysis>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<AlertDigest> AlertDigests => Set<AlertDigest>();
    public DbSet<AlertMatch> AlertMatches => Set<AlertMatch>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BoeRadarDbContext).Assembly);
    }
}
