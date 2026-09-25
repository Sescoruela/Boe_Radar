using BoeRadar.Application;
using BoeRadar.Domain;

namespace BoeRadar.Infrastructure.Analysis;

internal sealed class HeuristicDocumentAnalyzer : IDocumentAnalyzer
{
    public string Method => "heuristic";

    public string ModelName => "deterministic-rules-v1";

    public string PromptVersion => "radar-v1";

    public Task<RadarAnalysisOutput> AnalyzeAsync(
        AnalysisCandidate candidate,
        DocumentText content,
        CancellationToken cancellationToken = default)
    {
        var category = InferCategory(candidate.Title);
        var quote = ExtractEvidence(content.Text);
        var summary = candidate.Title.Trim().TrimEnd('.');
        var output = new RadarAnalysisOutput(
            true,
            category,
            summary + ".",
            [],
            [],
            [new RadarEvidence(quote, "relevancia")],
            0.55m);

        return Task.FromResult(output);
    }

    private static RadarCategory InferCategory(string title)
    {
        var normalized = title.ToLowerInvariant();
        if (normalized.Contains("subvención", StringComparison.Ordinal))
        {
            return RadarCategory.Subsidy;
        }

        if (normalized.Contains("ayuda", StringComparison.Ordinal))
        {
            return RadarCategory.Grant;
        }

        if (normalized.Contains("tribut", StringComparison.Ordinal) ||
            normalized.Contains("impuesto", StringComparison.Ordinal) ||
            normalized.Contains("fiscal", StringComparison.Ordinal))
        {
            return RadarCategory.Tax;
        }

        if (normalized.Contains("cotización", StringComparison.Ordinal) ||
            normalized.Contains("laboral", StringComparison.Ordinal))
        {
            return RadarCategory.Employment;
        }

        return RadarCategory.Obligation;
    }

    private static string ExtractEvidence(string text)
    {
        const int maximumLength = 280;
        if (text.Length <= maximumLength)
        {
            return text;
        }

        var cut = text.LastIndexOf(' ', maximumLength);
        return text[..(cut > 0 ? cut : maximumLength)];
    }
}
