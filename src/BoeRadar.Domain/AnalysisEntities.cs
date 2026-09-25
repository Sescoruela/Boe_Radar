namespace BoeRadar.Domain;

public enum RadarCategory
{
    Grant,
    Subsidy,
    Tax,
    Obligation,
    Employment,
    Financing,
    Other
}

public sealed class DocumentAnalysis
{
    private DocumentAnalysis()
    {
    }

    public Guid Id { get; private set; }

    public Guid DocumentId { get; private set; }

    public string ContentHash { get; private set; } = string.Empty;

    public bool IsRelevant { get; private set; }

    public RadarCategory Category { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public string RequirementsJson { get; private set; } = "[]";

    public string DeadlinesJson { get; private set; } = "[]";

    public string EvidenceJson { get; private set; } = "[]";

    public decimal Confidence { get; private set; }

    public string Method { get; private set; } = string.Empty;

    public string ModelName { get; private set; } = string.Empty;

    public string PromptVersion { get; private set; } = string.Empty;

    public int? InputTokens { get; private set; }

    public int? OutputTokens { get; private set; }

    public DateTimeOffset AnalyzedAt { get; private set; }

    public static DocumentAnalysis Create(
        Guid documentId,
        string contentHash,
        bool isRelevant,
        RadarCategory category,
        string summary,
        string requirementsJson,
        string deadlinesJson,
        string evidenceJson,
        decimal confidence,
        string method,
        string modelName,
        string promptVersion,
        int? inputTokens,
        int? outputTokens,
        DateTimeOffset analyzedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        ArgumentException.ThrowIfNullOrWhiteSpace(promptVersion);

        if (confidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence));
        }

        return new DocumentAnalysis
        {
            Id = Guid.CreateVersion7(),
            DocumentId = documentId,
            ContentHash = contentHash,
            IsRelevant = isRelevant,
            Category = category,
            Summary = summary,
            RequirementsJson = requirementsJson,
            DeadlinesJson = deadlinesJson,
            EvidenceJson = evidenceJson,
            Confidence = confidence,
            Method = method,
            ModelName = modelName,
            PromptVersion = promptVersion,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            AnalyzedAt = analyzedAt
        };
    }
}
