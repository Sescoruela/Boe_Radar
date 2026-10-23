using BoeRadar.Application;
using BoeRadar.Domain;
using BoeRadar.Infrastructure.Analysis;

namespace BoeRadar.UnitTests;

public sealed class ActionableAnalysisV3Tests
{
    private const string Payload = """
        {"isRelevant":true,"summary":"Deducción fiscal","requirements":[],"deadlines":[],
         "evidence":[{"quote":"Se establece una deducción","supports":"whatChanged"},
                     {"quote":"Se establece una deducción","supports":"impactKind"}],"confidence":0.9,
         "actionable":{"topic":"Tax","impactKind":"Opportunity","whatChanged":"Deducción fiscal",
         "affectedGroups":[],"nextSteps":["Comprueba las condiciones"],"uncertainties":["Aplicabilidad no confirmada"]}}
        """;

    [Fact]
    public void ParsesGeminiV3WithoutConflatingTaxAndObligation()
    {
        var output = GeminiDocumentAnalyzer.ParseOutput(Payload, 100, 50);
        Assert.Equal(RadarCategory.Tax, output.Category);
        Assert.Equal(ImpactKind.Opportunity, output.Actionable!.ImpactKind);
        Assert.Equal(100, output.InputTokens);
        new AnalysisValidator().Validate(output, "Se establece una deducción");
    }

    [Theory]
    [InlineData("\"topic\":\"Tax\",", "")]
    [InlineData("\"impactKind\":\"Opportunity\"", "\"impactKind\":12")]
    [InlineData("\"topic\":\"Tax\"", "\"topic\":\"Invented\"")]
    public void RejectsMissingOrUnknownGeminiClassification(string before, string after) =>
        Assert.Throws<System.Text.Json.JsonException>(() => GeminiDocumentAnalyzer.ParseOutput(Payload.Replace(before, after)));

    private const string Source = "Las pymes podrán solicitar ayudas. Efectos el 15 de octubre de 2026.";
    private static RadarAnalysisOutput Valid() => new(true, RadarCategory.Grant, "Ayudas para pymes.", [],
        [new("2026-10-15", "Entrada en vigor", true, "EffectiveDate")],
        [new("Las pymes podrán solicitar ayudas.", "whatChanged"),
         new("Las pymes podrán solicitar ayudas.", "affectedGroups[0]"),
         new("Las pymes podrán solicitar ayudas.", "impactKind"),
         new("Efectos el 15 de octubre de 2026.", "deadlines[0]")], .9m,
        Actionable: new(RadarTopic.Grants, ImpactKind.Opportunity, "Ayudas para pymes.", ["Pymes"],
            ["Revisa las bases completas."], ["No se confirma que cumplas los requisitos."]));

    [Fact]
    public void AcceptsIndependentTopicAndImpactWithTypedDates() =>
        new AnalysisValidator().Validate(Valid(), Source);

    [Theory]
    [InlineData("whatChanged")]
    [InlineData("affectedGroups[0]")]
    [InlineData("impactKind")]
    public void RejectsUnsupportedActionableClaims(string missing)
    {
        var output = Valid();
        Assert.Throws<InvalidOperationException>(() => new AnalysisValidator().Validate(output with
        { Evidence = output.Evidence.Where(item => item.Supports != missing).ToArray() }, Source));
    }

    [Fact]
    public void RejectsPublicationDateMasqueradingAsDeadlineType()
    {
        var output = Valid() with { Deadlines = [new("2026-10-15", "Publicación", true, "PublicationDate")] };
        Assert.Throws<InvalidOperationException>(() => new AnalysisValidator().Validate(output, Source));
    }

    [Fact]
    public void RejectsRelevantAnalysisWithoutUncertaintiesOrNextSteps()
    {
        var output = Valid();
        Assert.Throws<InvalidOperationException>(() => new AnalysisValidator().Validate(output with
        { Actionable = output.Actionable! with { Uncertainties = [] } }, Source));
        Assert.Throws<InvalidOperationException>(() => new AnalysisValidator().Validate(output with
        { Actionable = output.Actionable! with { NextSteps = [] } }, Source));
    }

    [Fact]
    public async Task HeuristicDoesNotInferObligationFromUnclassifiedTitle()
    {
        var analyzer = new HeuristicDocumentAnalyzer();
        var output = await analyzer.AnalyzeAsync(new(Guid.NewGuid(), "BOE-A-2026-1", new(2026, 10, 1),
            "Resolución administrativa", "Ministerio", "1", null, null), new(Source, "hash", "xml"));
        Assert.Equal("radar-v3", analyzer.PromptVersion);
        Assert.Equal(RadarCategory.Other, output.Category);
        Assert.Equal(ImpactKind.Information, output.Actionable!.ImpactKind);
        new AnalysisValidator().Validate(output, Source);
    }
}
