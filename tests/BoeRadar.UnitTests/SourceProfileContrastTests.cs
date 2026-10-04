using BoeRadar.Application;

namespace BoeRadar.UnitTests;

public sealed class SourceProfileContrastTests
{
    private static ActionableSourceReview Review(params string[] quotes) =>
        new("hash", DateTimeOffset.UtcNow, [new("recipients", "Destinatarios", quotes)]);

    [Fact]
    public void BodyCanSupportSelfEmploymentWithoutTitleMention()
    {
        const string quote = "Las personas físicas que desarrollen actividades económicas deberán estar dadas de alta.";
        var review = Review(quote);
        var contrast = SourceProfileContrastBuilder.Build(new("autonomous", "retail", "baleares"), review);
        var type = contrast.Dimensions.Single(item => item.Key == "businessType");
        Assert.Equal("mention", type.Status);
        Assert.Equal(quote, Assert.Single(type.Quotes));
        Assert.Equal("hash", contrast.SourceHash);
        Assert.Null(review.ProfileContrast);
        Assert.Contains("no confirma", type.Message);
    }

    [Theory]
    [InlineData("Organismos autónomos de la comunidad autónoma de Galicia.")]
    [InlineData("Las empresas podrán ser beneficiarias.")]
    public void BroadCompanyOrAutonomousInstitutionMentionDoesNotConfirmType(string quote)
    {
        var contrast = SourceProfileContrastBuilder.Build(new("autonomous", "retail", "all"), Review(quote));
        Assert.Equal("unknown", contrast.Dimensions.Single(item => item.Key == "businessType").Status);
    }

    [Fact]
    public void NegativeRecipientQuoteIsNotTurnedIntoEligibility()
    {
        var contrast = SourceProfileContrastBuilder.Build(new("sme", "technology", "all"),
            Review("No podrán ser beneficiarias las pymes que incumplan los requisitos."));
        var type = contrast.Dimensions.Single(item => item.Key == "businessType");
        Assert.Equal("mention", type.Status);
        Assert.Contains("No podrán", Assert.Single(type.Quotes));
        Assert.Contains("exclusiones", type.Message);
    }

    [Fact]
    public void AccentsAndUnknownTerritoryRemainExplicit()
    {
        var contrast = SourceProfileContrastBuilder.Build(new("sme", "technology", "canarias"),
            Review("Subvenciones de innovación para pequeñas y medianas empresas."));
        Assert.Equal("mention", contrast.Dimensions.Single(item => item.Key == "activity").Status);
        Assert.Equal("unknown", contrast.Dimensions.Single(item => item.Key == "territory").Status);
        Assert.Contains("No significa que esté excluido", contrast.Dimensions.Single(item => item.Key == "territory").Message);
    }

    [Fact]
    public void UnspecifiedProfileAndEmptyEvidenceDoNotInventCoverage()
    {
        var contrast = SourceProfileContrastBuilder.Build(new("sme", "other", "all"), Review());
        Assert.Equal(2, contrast.Dimensions.Count(item => item.Status == "notSpecified"));
        Assert.All(contrast.Dimensions, item => Assert.Empty(item.Quotes));
    }

    [Fact]
    public void SameSourceCanBeReviewedForDifferentProfilesWithoutLeakage()
    {
        var review = Review("Ayudas de formación a pymes.");
        var first = SourceProfileContrastBuilder.Build(new("sme", "educationSport", "all"), review);
        var second = SourceProfileContrastBuilder.Build(new("autonomous", "retail", "baleares"), review);
        Assert.Equal("mention", first.Dimensions[0].Status);
        Assert.Equal("unknown", second.Dimensions[0].Status);
        Assert.Null(review.ProfileContrast);
    }

    [Fact]
    public void InvalidProfileIsRejected() => Assert.Throws<ArgumentException>(() =>
        SourceProfileContrastBuilder.Build(new("invalid", "other", "all"), Review()));
}
