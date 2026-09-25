using System.Text.Json;
using BoeRadar.Application;
using BoeRadar.Domain;
using Microsoft.EntityFrameworkCore;

namespace BoeRadar.Infrastructure.Persistence;

internal sealed class EfIngestionStore(
    BoeRadarDbContext dbContext,
    TimeProvider timeProvider) : IIngestionStore
{
    public async Task<ImportIssueResult> ImportAsync(
        OfficialIssue issue,
        string trigger,
        CancellationToken cancellationToken = default)
    {
        var source = await dbContext.GazetteSources
            .SingleOrDefaultAsync(item => item.Code == issue.SourceCode, cancellationToken)
            ?? throw new InvalidOperationException(
                $"La fuente '{issue.SourceCode}' no está configurada.");
        var run = IngestionRun.Start(
            source.Id,
            issue.PublicationDate,
            trigger,
            timeProvider.GetUtcNow());
        dbContext.IngestionRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await using var transaction = await dbContext.Database
                .BeginTransactionAsync(cancellationToken);
            var externalIds = issue.Documents
                .Select(document => document.ExternalId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var existingDocuments = await dbContext.SourceDocuments
                .Where(document =>
                    document.SourceId == source.Id && externalIds.Contains(document.ExternalId))
                .ToDictionaryAsync(
                    document => document.ExternalId,
                    StringComparer.OrdinalIgnoreCase,
                    cancellationToken);
            var existingIssues = await dbContext.PublicationIssues
                .Where(item =>
                    item.SourceId == source.Id && item.PublicationDate == issue.PublicationDate)
                .ToDictionaryAsync(
                    item => item.IssueNumber,
                    StringComparer.OrdinalIgnoreCase,
                    cancellationToken);
            var created = 0;
            var updated = 0;
            var unchanged = 0;

            foreach (var issueNumber in issue.Documents
                         .Select(document => document.IssueNumber)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (existingIssues.ContainsKey(issueNumber))
                {
                    continue;
                }

                var externalIssueId =
                    $"{issue.SourceCode}-S-{issue.PublicationDate.Year}-{issueNumber}";
                var publicationIssue = PublicationIssue.Create(
                    source.Id,
                    externalIssueId,
                    issueNumber,
                    issue.PublicationDate,
                    issue.SummaryUrl.AbsoluteUri,
                    timeProvider.GetUtcNow());
                dbContext.PublicationIssues.Add(publicationIssue);
                existingIssues.Add(issueNumber, publicationIssue);
            }

            foreach (var incoming in issue.Documents)
            {
                var publicationIssue = existingIssues[incoming.IssueNumber];
                var now = timeProvider.GetUtcNow();

                if (!existingDocuments.TryGetValue(incoming.ExternalId, out var document))
                {
                    document = SourceDocument.Create(
                        publicationIssue.Id,
                        source.Id,
                        incoming.ExternalId,
                        issue.PublicationDate,
                        incoming.Title,
                        incoming.DepartmentCode,
                        incoming.DepartmentName,
                        incoming.SectionCode,
                        incoming.SectionName,
                        incoming.Epigraph,
                        incoming.ControlNumber,
                        incoming.OfficialHtmlUrl?.AbsoluteUri,
                        incoming.OfficialXmlUrl?.AbsoluteUri,
                        incoming.OfficialPdfUrl?.AbsoluteUri,
                        now);
                    dbContext.SourceDocuments.Add(document);
                    existingDocuments.Add(incoming.ExternalId, document);
                    created++;
                    continue;
                }

                var changed = document.Update(
                    publicationIssue.Id,
                    incoming.Title,
                    incoming.DepartmentCode,
                    incoming.DepartmentName,
                    incoming.SectionCode,
                    incoming.SectionName,
                    incoming.Epigraph,
                    incoming.ControlNumber,
                    incoming.OfficialHtmlUrl?.AbsoluteUri,
                    incoming.OfficialXmlUrl?.AbsoluteUri,
                    incoming.OfficialPdfUrl?.AbsoluteUri,
                    now);
                if (changed)
                {
                    updated++;
                }
                else
                {
                    unchanged++;
                }
            }

            run.Complete(
                JsonSerializer.Serialize(new
                {
                    discovered = issue.Documents.Count,
                    created,
                    updated,
                    unchanged
                }),
                timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new ImportIssueResult(
                run.Id,
                issue.PublicationDate,
                issue.Documents.Count,
                created,
                updated,
                unchanged);
        }
        catch (Exception exception)
        {
            dbContext.ChangeTracker.Clear();
            var persistedRun = await dbContext.IngestionRuns
                .SingleAsync(item => item.Id == run.Id, cancellationToken);
            persistedRun.Fail(exception.Message, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }
}
