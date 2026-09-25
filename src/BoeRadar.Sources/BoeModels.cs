namespace BoeRadar.Sources;

public sealed record BoeIssueSummary(
    DateOnly PublicationDate,
    IReadOnlyList<BoeDocumentItem> Documents);

public sealed record BoeDocumentItem(
    string Identifier,
    DateOnly PublicationDate,
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

public sealed record OfficialDocumentContent(
    string Format,
    string NormalizedText,
    string Sha256,
    DateTimeOffset FetchedAt);

public sealed class BoeSourceFormatException : Exception
{
    public BoeSourceFormatException(string message)
        : base(message)
    {
    }

    public BoeSourceFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class BoeSourceUnavailableException : Exception
{
    public BoeSourceUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

