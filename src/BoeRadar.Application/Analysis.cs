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

public sealed record DocumentText(
    string Text,
    string Sha256,
    string Format,
    IReadOnlyList<string>? Passages = null,
    string? OfficialTitle = null,
    IReadOnlyList<DocumentReference>? References = null);

public sealed record DocumentReference(string ExternalId, string Relation, string Description,
    string Direction, string OfficialUrl);

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
    string PromptVersion,
    IReadOnlyList<AnalysisFailure>? Failures = null,
    string? NextCursor = null);

public sealed record AnalysisFailure(string ExternalId, string ErrorType);

public sealed class AnalysisSourceTooLongException(int length, int maximum)
    : InvalidOperationException($"La fuente tiene {length} caracteres y supera el límite de {maximum}; no se analizará un texto truncado.");

public interface IAnalysisCandidateStore
{
    Task<IReadOnlyList<AnalysisCandidate>> GetByDateAsync(
        DateOnly publicationDate,
        int limit,
        CancellationToken cancellationToken = default,
        string? afterExternalId = null);

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
        if (!Enum.IsDefined(output.Category) || output.Requirements is null ||
            output.Deadlines is null || output.Evidence is null)
            throw new InvalidOperationException("La estructura del análisis no es válida.");

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
                !sourceText.Contains(evidence.Quote, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("La evidencia no aparece literalmente en la fuente.");
            }
        }

        for (var index = 0; index < output.Requirements.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(output.Requirements[index]) ||
                !output.Evidence.Any(item => item.Supports == $"requirements[{index}]" &&
                    item.Quote == output.Requirements[index]))
                throw new InvalidOperationException("Cada requisito necesita una cita específica de la fuente.");
        }

        for (var index = 0; index < output.Deadlines.Count; index++)
        {
            var deadline = output.Deadlines[index];
            var quotes = output.Evidence.Where(item => item.Supports == $"deadlines[{index}]")
                .Select(item => item.Quote).ToArray();
            if (string.IsNullOrWhiteSpace(deadline.Description) || quotes.Length == 0)
                throw new InvalidOperationException("Cada plazo necesita una cita específica de la fuente.");
            if (!deadline.IsExplicit)
            {
                if (deadline.Date is not null)
                    throw new InvalidOperationException("Un plazo relativo no puede contener una fecha calculada.");
                continue;
            }

            if (!DateOnly.TryParseExact(deadline.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date) ||
                !quotes.Any(quote => ContainsDate(quote, date)))
                throw new InvalidOperationException("La fecha ISO debe aparecer completa en la cita del plazo, incluido el año.");
        }
    }

    private static bool ContainsDate(string quote, DateOnly date)
    {
        // Accept only complete written dates, never infer a year from publication metadata.
        var formats = new[] { "yyyy-MM-dd", "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "dd-MM-yyyy",
            "d 'de' MMMM 'de' yyyy", "d 'de' MMMM yyyy" };
        return formats.Any(format => System.Text.RegularExpressions.Regex.IsMatch(quote,
            $@"(?<!\d){System.Text.RegularExpressions.Regex.Escape(date.ToString(format, CultureInfo.GetCultureInfo("es-ES")))}(?!\d)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant));
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
        CancellationToken cancellationToken = default,
        string? afterExternalId = null)
    {
        var budget = Math.Clamp(limit, 1, 500);
        var scanned = 0;
        var candidates = 0;
        var analyzed = 0;
        var skipped = 0;
        var failures = new List<AnalysisFailure>();
        var cursor = afterExternalId;

        while (true)
        {
            var documents = await store.GetByDateAsync(publicationDate, 100, cancellationToken, cursor);
            if (documents.Count == 0) break;
            foreach (var candidate in documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                cursor = candidate.ExternalId;
                scanned++;
                if (!prefilter.Evaluate(candidate).IsCandidate || candidate.OfficialXmlUrl is null)
                {
                    skipped++;
                    continue;
                }

                candidates++;
                try
                {
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
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception exception)
                {
                    // Report failed IDs without leaking provider payloads, credentials or document contents.
                    failures.Add(new(candidate.ExternalId, exception.GetType().Name));
                }
                if (analyzed + failures.Count >= budget)
                    return new(publicationDate, scanned, candidates, analyzed, skipped, analyzer.Method,
                        analyzer.ModelName, analyzer.PromptVersion, failures, cursor);
            }
        }

        return new AnalyzeBatchResult(
            publicationDate,
            scanned,
            candidates,
            analyzed,
            skipped,
            analyzer.Method,
            analyzer.ModelName,
            analyzer.PromptVersion,
            failures);
    }
}
