using System.Text.Json;
using BoeRadar.Application;
using BoeRadar.Domain;

namespace BoeRadar.UnitTests;

public sealed class RadarAnalysisTests
{
    [Fact]
    public void GoldenSet_PrefilterMatchesExpectedLabels()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "radar-golden-set.json"));
        var examples = JsonSerializer.Deserialize<GoldenExample[]>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var filter = new CandidatePrefilter();

        foreach (var example in examples)
        {
            var candidate = new AnalysisCandidate(
                Guid.NewGuid(),
                example.ExternalId,
                new DateOnly(2026, 1, 1),
                example.Title,
                example.Department,
                example.SectionCode,
                null,
                new Uri($"https://www.boe.es/{example.ExternalId}.xml"));

            Assert.Equal(example.ExpectedCandidate, filter.Evaluate(candidate).IsCandidate);
        }
    }

    [Fact]
    public void Validator_AcceptsLiteralEvidenceAndExplicitIsoDate()
    {
        var output = new RadarAnalysisOutput(
            true,
            RadarCategory.Grant,
            "Se convoca una ayuda.",
            ["Ser una pyme"],
            [new RadarDeadline("2026-10-15", "Fin de solicitudes", true)],
            [new RadarEvidence("Se convocan ayudas para pymes", "relevancia")],
            0.9m);

        new AnalysisValidator().Validate(
            output,
            "Se convocan ayudas para pymes. El plazo finaliza el 15 de octubre.");
    }

    [Fact]
    public void Validator_RejectsEvidenceNotFoundInSource()
    {
        var output = new RadarAnalysisOutput(
            true,
            RadarCategory.Grant,
            "Se convoca una ayuda.",
            [],
            [],
            [new RadarEvidence("Texto inventado", "relevancia")],
            0.9m);

        var error = Assert.Throws<InvalidOperationException>(() =>
            new AnalysisValidator().Validate(output, "Texto oficial distinto."));

        Assert.Contains("evidencia", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DocumentAnalysis_RejectsConfidenceOutsideRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DocumentAnalysis.Create(
            Guid.NewGuid(),
            new string('a', 64),
            true,
            RadarCategory.Tax,
            "Resumen",
            "[]",
            "[]",
            "[]",
            1.1m,
            "gemini",
            "model",
            "radar-v1",
            null,
            null,
            DateTimeOffset.UtcNow));
    }

    private sealed record GoldenExample(
        string ExternalId,
        string Title,
        string Department,
        string SectionCode,
        bool ExpectedCandidate);
}
