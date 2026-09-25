namespace BoeRadar.Domain;

public sealed class GazetteSource
{
    public static readonly Guid BoeId = Guid.Parse("b0e00000-0000-7000-8000-000000000001");

    private GazetteSource()
    {
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string BaseUrl { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}

public sealed class PublicationIssue
{
    private PublicationIssue()
    {
    }

    private PublicationIssue(
        Guid sourceId,
        string externalId,
        string issueNumber,
        DateOnly publicationDate,
        string sourceUrl,
        DateTimeOffset discoveredAt)
    {
        Id = Guid.CreateVersion7();
        SourceId = sourceId;
        ExternalId = externalId;
        IssueNumber = issueNumber;
        PublicationDate = publicationDate;
        SourceUrl = sourceUrl;
        RawMetadata = "{}";
        DiscoveredAt = discoveredAt;
    }

    public Guid Id { get; private set; }

    public Guid SourceId { get; private set; }

    public string ExternalId { get; private set; } = string.Empty;

    public string IssueNumber { get; private set; } = string.Empty;

    public DateOnly PublicationDate { get; private set; }

    public string SourceUrl { get; private set; } = string.Empty;

    public string RawMetadata { get; private set; } = "{}";

    public DateTimeOffset DiscoveredAt { get; private set; }

    public static PublicationIssue Create(
        Guid sourceId,
        string externalId,
        string issueNumber,
        DateOnly publicationDate,
        string sourceUrl,
        DateTimeOffset discoveredAt) =>
        new(sourceId, externalId, issueNumber, publicationDate, sourceUrl, discoveredAt);
}

public sealed class SourceDocument
{
    private SourceDocument()
    {
    }

    private SourceDocument(
        Guid issueId,
        Guid sourceId,
        string externalId,
        DateOnly publicationDate,
        string title,
        string departmentCode,
        string department,
        string sectionCode,
        string sectionName,
        string? epigraph,
        string? controlNumber,
        string? officialHtmlUrl,
        string? officialXmlUrl,
        string? officialPdfUrl,
        DateTimeOffset now)
    {
        Id = Guid.CreateVersion7();
        IssueId = issueId;
        SourceId = sourceId;
        ExternalId = externalId;
        PublicationDate = publicationDate;
        CreatedAt = now;
        _ = Update(
            issueId,
            title,
            departmentCode,
            department,
            sectionCode,
            sectionName,
            epigraph,
            controlNumber,
            officialHtmlUrl,
            officialXmlUrl,
            officialPdfUrl,
            now);
    }

    public Guid Id { get; private set; }

    public Guid IssueId { get; private set; }

    public Guid SourceId { get; private set; }

    public string ExternalId { get; private set; } = string.Empty;

    public DateOnly PublicationDate { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string DepartmentCode { get; private set; } = string.Empty;

    public string Department { get; private set; } = string.Empty;

    public string SectionCode { get; private set; } = string.Empty;

    public string SectionName { get; private set; } = string.Empty;

    public string? Epigraph { get; private set; }

    public string? ControlNumber { get; private set; }

    public string? OfficialHtmlUrl { get; private set; }

    public string? OfficialXmlUrl { get; private set; }

    public string? OfficialPdfUrl { get; private set; }

    public string RawMetadata { get; private set; } = "{}";

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static SourceDocument Create(
        Guid issueId,
        Guid sourceId,
        string externalId,
        DateOnly publicationDate,
        string title,
        string departmentCode,
        string department,
        string sectionCode,
        string sectionName,
        string? epigraph,
        string? controlNumber,
        string? officialHtmlUrl,
        string? officialXmlUrl,
        string? officialPdfUrl,
        DateTimeOffset now) =>
        new(
            issueId,
            sourceId,
            externalId,
            publicationDate,
            title,
            departmentCode,
            department,
            sectionCode,
            sectionName,
            epigraph,
            controlNumber,
            officialHtmlUrl,
            officialXmlUrl,
            officialPdfUrl,
            now);

    public bool Update(
        Guid issueId,
        string title,
        string departmentCode,
        string department,
        string sectionCode,
        string sectionName,
        string? epigraph,
        string? controlNumber,
        string? officialHtmlUrl,
        string? officialXmlUrl,
        string? officialPdfUrl,
        DateTimeOffset now)
    {
        var hasChanges =
            IssueId != issueId ||
            !string.Equals(Title, title, StringComparison.Ordinal) ||
            !string.Equals(DepartmentCode, departmentCode, StringComparison.Ordinal) ||
            !string.Equals(Department, department, StringComparison.Ordinal) ||
            !string.Equals(SectionCode, sectionCode, StringComparison.Ordinal) ||
            !string.Equals(SectionName, sectionName, StringComparison.Ordinal) ||
            !string.Equals(Epigraph, epigraph, StringComparison.Ordinal) ||
            !string.Equals(ControlNumber, controlNumber, StringComparison.Ordinal) ||
            !string.Equals(OfficialHtmlUrl, officialHtmlUrl, StringComparison.Ordinal) ||
            !string.Equals(OfficialXmlUrl, officialXmlUrl, StringComparison.Ordinal) ||
            !string.Equals(OfficialPdfUrl, officialPdfUrl, StringComparison.Ordinal);

        if (!hasChanges)
        {
            return false;
        }

        IssueId = issueId;
        Title = title;
        DepartmentCode = departmentCode;
        Department = department;
        SectionCode = sectionCode;
        SectionName = sectionName;
        Epigraph = epigraph;
        ControlNumber = controlNumber;
        OfficialHtmlUrl = officialHtmlUrl;
        OfficialXmlUrl = officialXmlUrl;
        OfficialPdfUrl = officialPdfUrl;
        UpdatedAt = now;
        return true;
    }
}

public sealed class DocumentContent
{
    private DocumentContent()
    {
    }

    public Guid Id { get; private set; }

    public Guid DocumentId { get; private set; }

    public string ContentHash { get; private set; } = string.Empty;

    public string Format { get; private set; } = string.Empty;

    public string NormalizedText { get; private set; } = string.Empty;

    public string? StorageUri { get; private set; }

    public DateTimeOffset FetchedAt { get; private set; }

    public bool IsCurrent { get; private set; }
}
