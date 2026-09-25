using BoeRadar.Application;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BoeRadar.Infrastructure.Persistence;

internal sealed class EfPublicationCatalog(BoeRadarDbContext dbContext)
    : IPublicationCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CatalogStatus> GetStatusAsync(
        bool emailAlertsEnabled,
        CancellationToken cancellationToken = default)
    {
        var latestDate = await dbContext.SourceDocuments.AsNoTracking()
            .MaxAsync(document => (DateOnly?)document.PublicationDate, cancellationToken);
        var total = await dbContext.SourceDocuments.AsNoTracking().CountAsync(cancellationToken);
        return new CatalogStatus(latestDate, total, emailAlertsEnabled);
    }

    public async Task<PagedResult<PublicationListItem>> SearchAsync(
        PublicationSearch search,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(search.Page, 1);
        var pageSize = Math.Clamp(search.PageSize, 1, 100);
        var query = dbContext.SourceDocuments.AsNoTracking();

        if (search.BusinessSignalsOnly)
        {
            query = query.Where(document =>
                (document.SectionCode == "5B" &&
                 (EF.Functions.ILike(document.Title, "%ayuda%") ||
                  EF.Functions.ILike(document.Title, "%subvenci%") ||
                  EF.Functions.ILike(document.Title, "%bonificaci%") ||
                  EF.Functions.ILike(document.Title, "%financiaci%") ||
                  EF.Functions.ILike(document.Title, "%préstamo%")) &&
                 !EF.Functions.ILike(document.Title, "%ayuda al estudio%") &&
                 !EF.Functions.ILike(document.Title, "%beca%")) ||
                (document.SectionCode == "1" &&
                 (EF.Functions.ILike(document.Title, "%tribut%") ||
                  EF.Functions.ILike(document.Title, "%impuesto%") ||
                  EF.Functions.ILike(document.Title, "%cotizaci%") ||
                  EF.Functions.ILike(document.Title, "%autónom%") ||
                  EF.Functions.ILike(document.Title, "%empresa%"))));
        }

        if (!string.IsNullOrWhiteSpace(search.Query))
        {
            var pattern = $"%{search.Query.Trim()}%";
            query = query.Where(document =>
                EF.Functions.ILike(document.Title, pattern) ||
                EF.Functions.ILike(document.Department, pattern) ||
                (document.Epigraph != null && EF.Functions.ILike(document.Epigraph, pattern)));
        }

        if (search.DateFrom is { } dateFrom)
        {
            query = query.Where(document => document.PublicationDate >= dateFrom);
        }

        if (search.DateTo is { } dateTo)
        {
            query = query.Where(document => document.PublicationDate <= dateTo);
        }

        if (!string.IsNullOrWhiteSpace(search.Section))
        {
            query = query.Where(document => document.SectionCode == search.Section);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(document => document.PublicationDate)
            .ThenBy(document => document.ExternalId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(document => new PublicationListItem(
                document.Id,
                document.ExternalId,
                document.PublicationDate,
                document.Title,
                document.SectionCode,
                document.SectionName,
                document.Department,
                document.Epigraph,
                document.OfficialPdfUrl,
                null))
            .ToArrayAsync(cancellationToken);

        var documentIds = items.Select(item => item.Id).ToArray();
        var analyses = await dbContext.DocumentAnalyses
            .AsNoTracking()
            .Where(analysis => documentIds.Contains(analysis.DocumentId))
            .OrderByDescending(analysis => analysis.AnalyzedAt)
            .ToArrayAsync(cancellationToken);
        var latestByDocument = analyses
            .GroupBy(analysis => analysis.DocumentId)
            .ToDictionary(group => group.Key, group => group.First());
        items = items
            .Select(item => latestByDocument.TryGetValue(item.Id, out var analysis)
                ? item with
                {
                    Analysis = new RadarAnalysisSummary(
                        analysis.IsRelevant,
                        analysis.Category.ToString(),
                        analysis.Summary,
                        analysis.Confidence,
                        analysis.Method)
                }
                : item)
            .ToArray();

        return new PagedResult<PublicationListItem>(
            items,
            page,
            pageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize));
    }

    public async Task<PublicationDetail?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var publication = await dbContext.SourceDocuments
            .AsNoTracking()
            .Where(document => document.Id == id)
            .Join(
                dbContext.PublicationIssues.AsNoTracking(),
                document => document.IssueId,
                issue => issue.Id,
                (document, issue) => new PublicationDetail(
                    document.Id,
                    document.ExternalId,
                    document.PublicationDate,
                    issue.IssueNumber,
                    document.Title,
                    document.SectionCode,
                    document.SectionName,
                    document.DepartmentCode,
                    document.Department,
                    document.Epigraph,
                    document.ControlNumber,
                    document.OfficialHtmlUrl,
                    document.OfficialXmlUrl,
                    document.OfficialPdfUrl,
                    null))
            .SingleOrDefaultAsync(cancellationToken);

        if (publication is null)
        {
            return null;
        }

        var analysis = await dbContext.DocumentAnalyses
            .AsNoTracking()
            .Where(item => item.DocumentId == id)
            .OrderByDescending(item => item.AnalyzedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (analysis is null)
        {
            return publication;
        }

        return publication with
        {
            Analysis = new RadarAnalysisDetail(
                analysis.IsRelevant,
                analysis.Category.ToString(),
                analysis.Summary,
                Deserialize<IReadOnlyList<string>>(analysis.RequirementsJson),
                Deserialize<IReadOnlyList<RadarDeadline>>(analysis.DeadlinesJson),
                Deserialize<IReadOnlyList<RadarEvidence>>(analysis.EvidenceJson),
                analysis.Confidence,
                analysis.Method,
                analysis.ModelName,
                analysis.PromptVersion,
                analysis.AnalyzedAt)
        };
    }

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions)
        ?? throw new InvalidOperationException("El análisis persistido contiene JSON inválido.");
}
