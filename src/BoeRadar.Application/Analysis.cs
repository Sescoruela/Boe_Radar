using BoeRadar.Domain;
using System.Globalization;
using System.Text;

namespace BoeRadar.Application;

public sealed record AnalysisCandidate(
    Guid DocumentId,
    string ExternalId,
    DateOnly PublicationDate,
    string Title,
    string Department,
    string SectionCode,
    string? Epigraph,
    Uri? OfficialXmlUrl);

public sealed record DocumentText(string Text, string Sha256, string Format);

public sealed record RadarDeadline(
    string? Date,
    string Description,
    bool IsExplicit);

public sealed record RadarEvidence(
    string Quote,
    string Supports);

public sealed record RadarAnalysisOutput(
    bool IsRelevant,
    RadarCategory Category,
    string Summary,
    IReadOnlyList<string> Requirements,
    IReadOnlyList<RadarDeadline> Deadlines,
    IReadOnlyList<RadarEvidence> Evidence,
    decimal Confidence,
    int? InputTokens = null,
    int? OutputTokens = null);

public sealed record PrefilterDecision(bool IsCandidate, int Score, IReadOnlyList<string> Reasons);

public sealed record AnalyzeBatchResult(
    DateOnly PublicationDate,
    int Scanned,
    int Candidates,
    int Analyzed,
    int Skipped,
    string Method,
    string ModelName,
    string PromptVersion);

public interface IAnalysisCandidateStore
{
    Task<IReadOnlyList<AnalysisCandidate>> GetByDateAsync(
        DateOnly publicationDate,
        int limit,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        Guid documentId,
        string contentHash,
        string modelName,
        string promptVersion,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        Guid documentId,
        string contentHash,
        RadarAnalysisOutput output,
        string method,
        string modelName,
        string promptVersion,
        CancellationToken cancellationToken = default);
}

public interface IOfficialDocumentTextSource
{
    Task<DocumentText> GetAsync(Uri officialXmlUrl, CancellationToken cancellationToken = default);
}

public interface IDocumentAnalyzer
{
    string Method { get; }

    string ModelName { get; }

    string PromptVersion { get; }

    Task<RadarAnalysisOutput> AnalyzeAsync(
        AnalysisCandidate candidate,
        DocumentText content,
        CancellationToken cancellationToken = default);
}

public sealed class CandidatePrefilter
{
    private static readonly (string Term, int Score)[] PositiveTerms =
    [
        ("ayuda", 4), ("subvencion", 4), ("convocatoria", 2), ("beneficiari", 2),
        ("autonom", 4), ("pyme", 4), ("tribut", 4), ("impuesto", 4),
        ("fiscal", 4), ("cotizacion", 4), ("obligacion", 4), ("plazo", 1),
        ("bonificacion", 4), ("financiacion", 4), ("prestamo", 3)
    ];

    private static readonly string[] NegativeTerms =
    [
        "nombramiento", "cese", "designa embajador", "administracion de justicia",
        "anuncio de formalizacion de contratos"
    ];

    public PrefilterDecision Evaluate(AnalysisCandidate candidate)
    {
        var haystack = RemoveDiacritics(
            string.Join(' ', candidate.Title, candidate.Department, candidate.Epigraph).ToLowerInvariant());
        var score = 0;
        var reasons = new List<string>();

        foreach (var (term, weight) in PositiveTerms)
        {
            if (!haystack.Contains(term, StringComparison.Ordinal))
            {
                continue;
            }

            score += weight;
            reasons.Add(term);
        }

        if (NegativeTerms.Any(term => haystack.Contains(term, StringComparison.Ordinal)))
        {
            score -= 5;
        }

        return new PrefilterDecision(score >= 4, score, reasons);
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                result.Append(character);
            }
        }

        return result.ToString().Normalize(NormalizationForm.FormC);
    }
}

public sealed class AnalysisValidator
{
    public void Validate(RadarAnalysisOutput output, string sourceText)
    {
        if (output.Confidence is < 0 or > 1)
        {
            throw new InvalidOperationException("La confianza debe estar entre 0 y 1.");
        }

        if (string.IsNullOrWhiteSpace(output.Summary))
        {
            throw new InvalidOperationException("El análisis debe incluir un resumen.");
        }

        if (output.IsRelevant && output.Evidence.Count == 0)
        {
            throw new InvalidOperationException("Un resultado relevante debe incluir evidencia.");
        }

        foreach (var evidence in output.Evidence)
        {
            if (string.IsNullOrWhiteSpace(evidence.Quote) ||
                !sourceText.Contains(evidence.Quote, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("La evidencia no aparece literalmente en la fuente.");
            }
        }

        foreach (var deadline in output.Deadlines.Where(deadline => deadline.IsExplicit))
        {
            if (string.IsNullOrWhiteSpace(deadline.Date) ||
                !DateOnly.TryParse(deadline.Date, out _))
            {
                throw new InvalidOperationException("Los plazos explícitos deben usar una fecha ISO válida.");
            }
        }
    }
}

public sealed class AnalyzeRadarDocuments(
    IAnalysisCandidateStore store,
    IOfficialDocumentTextSource textSource,
    IDocumentAnalyzer analyzer,
    CandidatePrefilter prefilter,
    AnalysisValidator validator)
{
    public async Task<AnalyzeBatchResult> ExecuteAsync(
        DateOnly publicationDate,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var documents = await store.GetByDateAsync(publicationDate, Math.Clamp(limit, 1, 500), cancellationToken);
        var candidates = 0;
        var analyzed = 0;
        var skipped = 0;

        foreach (var candidate in documents)
        {
            if (!prefilter.Evaluate(candidate).IsCandidate || candidate.OfficialXmlUrl is null)
            {
                skipped++;
                continue;
            }

            candidates++;
            var content = await textSource.GetAsync(candidate.OfficialXmlUrl, cancellationToken);
            if (await store.ExistsAsync(
                    candidate.DocumentId,
                    content.Sha256,
                    analyzer.ModelName,
                    analyzer.PromptVersion,
                    cancellationToken))
            {
                skipped++;
                continue;
            }

            var output = await analyzer.AnalyzeAsync(candidate, content, cancellationToken);
            validator.Validate(output, content.Text);
            await store.SaveAsync(
                candidate.DocumentId,
                content.Sha256,
                output,
                analyzer.Method,
                analyzer.ModelName,
                analyzer.PromptVersion,
                cancellationToken);
            analyzed++;
        }

        return new AnalyzeBatchResult(
            publicationDate,
            documents.Count,
            candidates,
            analyzed,
            skipped,
            analyzer.Method,
            analyzer.ModelName,
            analyzer.PromptVersion);
    }
}
