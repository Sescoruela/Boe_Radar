using BoeRadar.Application;

namespace BoeRadar.UnitTests;

public sealed class SourceEvidenceRankingTests
{
    private static PublicationListItem Item() => new(Guid.NewGuid(), "BOE-A-2026-1",
        new DateOnly(2026, 9, 26), "Resolución general", "1", "Disposiciones generales", "Ministerio", null, null, null);
    private static ActionableSourceReview Review(string quote) =>
        new("hash", DateTimeOffset.UtcNow, [new("recipients", "Destinatarios", [quote])]);

    [Theory]
    [InlineData("autonomous", "other", "all", "Ayudas a trabajadores autónomos.", 40)]
    [InlineData("sme", "technology", "all", "Innovación para pymes.", 120)]
    [InlineData("sme", "retail", "baleares", "Comercio para pymes de Baleares.", 140)]
    public void EvidenceAddsExplainedPriorityWithoutClaimingEligibility(string type, string activity,
        string territory, string quote, int priority)
    {
        var match = SourceEvidenceRanking.Match(new(type, activity, territory), Item(), Review(quote));
        Assert.Equal(priority, match.Priority);
        Assert.Equal("hash", match.SourceHash);
        Assert.DoesNotContain(match.Reasons, reason => reason.Contains("faltan datos"));
        Assert.Contains(match.Checks, check => check.Contains("no confirman elegibilidad"));
    }

    [Theory]
    [InlineData("No podrán recibir ayudas las pymes excluidas.")]
    [InlineData("Ayudas excepto para pymes.")]
    [InlineData("Las pymes no tendrán acceso a esta convocatoria.")]
    public void RestrictionsDoNotAddPriority(string quote)
    {
        Assert.Equal(0, SourceEvidenceRanking.Match(new("sme", "other", "all"), Item(), Review(quote)).Priority);
    }

    [Fact]
    public void MissingOrUnrelatedEvidenceDoesNotRemoveUnknownSignals()
    {
        var profile = new BusinessProfile("autonomous", "retail", "canarias");
        Assert.Equal(0, SourceEvidenceRanking.Match(profile, Item(), null).Priority);
        var reviewed = SourceEvidenceRanking.Match(profile, Item(), Review("Organismos autónomos."));
        Assert.Equal(0, reviewed.Priority);
        Assert.Equal("Alcance por comprobar", reviewed.Label);
    }
}
