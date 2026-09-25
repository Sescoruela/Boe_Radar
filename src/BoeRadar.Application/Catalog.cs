namespace BoeRadar.Application;

public sealed record PublicationSearch(
    string? Query,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    string? Section,
    int Page = 1,
    int PageSize = 20,
    bool BusinessSignalsOnly = false);

public sealed record CatalogStatus(DateOnly? LatestPublicationDate, int TotalPublications, bool EmailAlertsEnabled);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record PublicationListItem(
    Guid Id,
    string ExternalId,
    DateOnly PublicationDate,
    string Title,
    string SectionCode,
    string SectionName,
    string Department,
    string? Epigraph,
    string? OfficialPdfUrl,
    RadarAnalysisSummary? Analysis);

public sealed record RadarAnalysisSummary(
    bool IsRelevant,
    string Category,
    string Summary,
    decimal Confidence,
    string Method);

public sealed record RadarAnalysisDetail(
    bool IsRelevant,
    string Category,
    string Summary,
    IReadOnlyList<string> Requirements,
    IReadOnlyList<RadarDeadline> Deadlines,
    IReadOnlyList<RadarEvidence> Evidence,
    decimal Confidence,
    string Method,
    string ModelName,
    string PromptVersion,
    DateTimeOffset AnalyzedAt);

public sealed record PublicationDetail(
    Guid Id,
    string ExternalId,
    DateOnly PublicationDate,
    string IssueNumber,
    string Title,
    string SectionCode,
    string SectionName,
    string DepartmentCode,
    string Department,
    string? Epigraph,
    string? ControlNumber,
    string? OfficialHtmlUrl,
    string? OfficialXmlUrl,
    string? OfficialPdfUrl,
    RadarAnalysisDetail? Analysis);

public interface IPublicationCatalog
{
    Task<CatalogStatus> GetStatusAsync(bool emailAlertsEnabled, CancellationToken cancellationToken = default);

    Task<PagedResult<PublicationListItem>> SearchAsync(
        PublicationSearch search,
        CancellationToken cancellationToken = default);

    Task<PublicationDetail?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
