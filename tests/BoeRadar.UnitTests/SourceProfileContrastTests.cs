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
        Assert.Equal("restriction", type.Status);
        Assert.Contains("No podrán", Assert.Single(type.Quotes));
        Assert.Contains("No confirma que estés excluido", type.Message);
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

    [Fact]
    public void UnrelatedAmountsDeadlinesAndApplicationPassagesAreNotProfileEvidence()
    {
        var review = new ActionableSourceReview("hash", DateTimeOffset.UtcNow,
            [new("amount", "Importes", ["Pymes: presupuesto para comercio de Canarias." ]),
             new("application", "Solicitud", ["El formulario digital de pymes se presenta en Canarias." ]),
             new("deadlines", "Plazos", ["Comercio de pymes de Canarias hasta el día 3." ])]);
        var contrast = SourceProfileContrastBuilder.Build(new("sme", "retail", "canarias"), review);
        Assert.All(contrast.Dimensions, dimension => Assert.Equal("unknown", dimension.Status));
    }

    [Fact]
    public void RestrictivePassagesTakePrecedenceOverPositiveMentionsAndKeepTheirEvidence()
    {
        const string restriction = "No podrán ser beneficiarias las pymes con deudas.";
        var contrast = SourceProfileContrastBuilder.Build(new("sme", "other", "all"), Review("Ayudas a pymes.", restriction));
        var type = contrast.Dimensions.Single(dimension => dimension.Key == "businessType");
        Assert.Equal("restriction", type.Status);
        Assert.Equal(restriction, Assert.Single(type.Quotes));
        Assert.Equal("source", type.Origin);
    }

    [Fact]
    public void NationalScopeIsAHintAndAnUnspecifiedTerritoryRemainsUnspecified()
    {
        var review = new ActionableSourceReview("hash", DateTimeOffset.UtcNow,
            [new("territory", "Ámbito", ["Ámbito estatal con condiciones específicas." ])]);
        var local = SourceProfileContrastBuilder.Build(new("sme", "other", "canarias"), review);
        Assert.Equal("mention", local.Dimensions.Single(dimension => dimension.Key == "territory").Status);
        var general = SourceProfileContrastBuilder.Build(new("sme", "other", "all"), review);
        Assert.Equal("notSpecified", general.Dimensions.Single(dimension => dimension.Key == "territory").Status);
    }

    [Theory]
    [InlineData("Serán beneficiarias exclusivamente las entidades locales.")]
    [InlineData("Destinatarios: únicamente ayuntamientos.")]
    public void ExplicitExclusivePublicRecipientsAreAPossibleMismatchNotAHiddenResult(string quote)
    {
        var contrast = SourceProfileContrastBuilder.Build(new("autonomous", "other", "all"), Review(quote));
        var type = contrast.Dimensions.Single(dimension => dimension.Key == "businessType");
        Assert.Equal("restriction", type.Status);
        Assert.Contains("posible desajuste", type.Message);
        Assert.Equal(quote, Assert.Single(type.Quotes));
    }

    [Theory]
    [InlineData("Entidades locales y otras entidades podrán solicitar las ayudas.")]
    [InlineData("El ayuntamiento informa a las empresas sobre las ayudas.")]
    public void NonexclusivePublicReferencesDoNotInventAMismatch(string quote)
    {
        var contrast = SourceProfileContrastBuilder.Build(new("sme", "other", "all"), Review(quote));
        Assert.Equal("unknown", contrast.Dimensions.Single(dimension => dimension.Key == "businessType").Status);
    }

    [Fact]
    public void ExplicitPhysicalPersonExclusionFlagsASelfEmploymentRestrictionWithTheOriginalQuote()
    {
        const string quote = "2. Quedan excluidas las personas físicas, las Administraciones Públicas y las entidades con ánimo de lucro.";
        var contrast = SourceProfileContrastBuilder.Build(new("autonomous", "technology", "all"), Review(quote));
        var type = contrast.Dimensions.Single(dimension => dimension.Key == "businessType");
        Assert.Equal("restriction", type.Status);
        Assert.Equal(quote, Assert.Single(type.Quotes));
        Assert.Contains("posible restricción", type.Message);
        var sme = SourceProfileContrastBuilder.Build(new("sme", "technology", "all"), Review(quote));
        Assert.Equal("unknown", sme.Dimensions.Single(dimension => dimension.Key == "businessType").Status);
    }

    [Fact]
    public void NegatedPhysicalPersonExclusionIsNotARestriction()
    {
        var contrast = SourceProfileContrastBuilder.Build(new("autonomous", "other", "all"),
            Review("No quedan excluidas las personas físicas."));
        Assert.Equal("unknown", contrast.Dimensions.Single(dimension => dimension.Key == "businessType").Status);
    }
}
