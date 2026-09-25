namespace BoeRadar.Application;

public sealed record OfficialIssue(
    string SourceCode,
    DateOnly PublicationDate,
    Uri SummaryUrl,
    IReadOnlyList<OfficialDocument> Documents);

public sealed record OfficialDocument(
    string ExternalId,
    string IssueNumber,
    string SectionCode,
    string SectionName,
    string DepartmentCode,
    string DepartmentName,
    string? Epigraph,
    string Title,
    string? ControlNumber,
    Uri? OfficialHtmlUrl,
    Uri? OfficialXmlUrl,
    Uri? OfficialPdfUrl);

public sealed record ImportIssueResult(
    Guid RunId,
    DateOnly PublicationDate,
    int Discovered,
    int Created,
    int Updated,
    int Unchanged);

public interface IOfficialGazetteSource
{
    string SourceCode { get; }

    Task<OfficialIssue> GetIssueAsync(
        DateOnly publicationDate,
        CancellationToken cancellationToken = default);
}

public interface IIngestionStore
{
    Task<ImportIssueResult> ImportAsync(
        OfficialIssue issue,
        string trigger,
        CancellationToken cancellationToken = default);
}

public sealed class ImportOfficialIssue(
    IOfficialGazetteSource source,
    IIngestionStore store)
{
    public async Task<ImportIssueResult> ExecuteAsync(
        DateOnly publicationDate,
        string trigger = "manual",
        CancellationToken cancellationToken = default)
    {
        var issue = await source.GetIssueAsync(publicationDate, cancellationToken);
        return await store.ImportAsync(
            issue,
            trigger,
            cancellationToken);
    }
}
