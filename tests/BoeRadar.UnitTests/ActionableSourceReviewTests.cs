using BoeRadar.Application;

namespace BoeRadar.UnitTests;

public sealed class ActionableSourceReviewTests
{
    [Fact]
    public void OfficialReferencesArePreservedOutsideBodyEvidenceAndProfileMatching()
    {
        var reference = new DocumentReference("BOE-A-2003-20151", "MODIFICA", "Normas de comercio",
            "previous", "https://www.boe.es/buscar/doc.php?id=BOE-A-2003-20151");
        var review = new ActionableSourceReviewBuilder().Build(
            new("Contenido sin menciones de actividad.", "hash", "xml", References: [reference]), DateTimeOffset.UtcNow);
        Assert.Equal(reference, Assert.Single(review.References));
        Assert.Equal("unknown", SourceProfileContrastBuilder.Build(new("sme", "retail", "all"), review)
            .Dimensions.Single(dimension => dimension.Key == "activity").Status);
        Assert.All(review.Groups, group => Assert.Empty(group.Quotes));
    }
    [Theory]
    [InlineData("Extracto por el que se convocan ayudas a pymes", "grant")]
    [InlineData("Bases reguladoras de subvenciones", "grant")]
    [InlineData("Ley por la que se modifica el Impuesto sobre Sociedades", "tax")]
    [InlineData("Acuerdo administrativo de Seguridad Social con Filipinas", "regulation")]
    [InlineData("Resolución de precios de venta al público de tabaco", "regulation")]
    [InlineData("Resolución de nombramiento de personal", "general")]
    public void TemplateUsesOfficialTitleNotIncidentalBodyTerms(string title, string expected)
    {
        const string text = "Se mencionan ayudas, subvenciones e impuestos en los antecedentes.";
        var review = new ActionableSourceReviewBuilder().Build(
            new DocumentText(text, "hash", "xml", OfficialTitle: title), DateTimeOffset.UtcNow);
        Assert.Equal(expected, review.Kind);
        Assert.NotEmpty(review.NextStep);
        if (expected is "regulation" or "tax")
            Assert.DoesNotContain(review.Groups, group => group.Key is "application" or "amount");
    }

    [Fact]
    public void RegulatoryObligationsAndEffectiveDateRemainLiteral()
    {
        string[] passages = ["Artículo 4. Los empresarios deberán comunicar los datos de sus trabajadores.",
            "Este acuerdo entrará en vigor el 1 de enero de 2027.", "Publicado el 22 de septiembre de 2026."];
        var text = string.Join(" ", passages);
        var review = new ActionableSourceReviewBuilder().Build(new DocumentText(text, "hash", "xml",
            passages, "Acuerdo administrativo de Seguridad Social"), DateTimeOffset.UtcNow);
        Assert.Equal("regulation", review.Kind);
        Assert.Contains(review.Groups.Single(group => group.Key == "obligations").Quotes,
            quote => quote.Contains("deberán comunicar"));
        var effective = Assert.Single(review.Groups.Single(group => group.Key == "effective").Quotes);
        Assert.Contains("1 de enero de 2027", effective);
        Assert.DoesNotContain("22 de septiembre", effective);
        Assert.All(review.Groups.SelectMany(group => group.Quotes), quote => Assert.Contains(quote, text));
    }

    [Fact]
    public void PublicationDateDoesNotBecomeEffectiveDate()
    {
        var review = new ActionableSourceReviewBuilder().Build(new DocumentText(
            "Publicado el 3 de octubre de 2026. El importe es de 50 euros.", "hash", "xml",
            OfficialTitle: "Resolución de precios de venta de tabaco"), DateTimeOffset.UtcNow);
        Assert.Empty(review.Groups.Single(group => group.Key == "effective").Quotes);
        Assert.DoesNotContain(review.Groups, group => group.Key == "application");
    }

    [Fact]
    public void GenericObligationMentionDoesNotDisplaceConcreteBusinessDuties()
    {
        string[] passages = ["Información sobre los derechos y obligaciones del convenio.",
            "El empleador deberá comunicar el cese de la relación laboral.",
            "El trabajador por cuenta propia deberá comunicar el fin de su actividad."];
        var review = new ActionableSourceReviewBuilder().Build(new DocumentText(string.Join(" ", passages),
            "hash", "xml", passages, "Acuerdo administrativo de Seguridad Social"), DateTimeOffset.UtcNow);
        var quotes = review.Groups.Single(group => group.Key == "obligations").Quotes;
        Assert.NotEmpty(quotes);
        Assert.Contains(quotes, quote => quote.Contains("empleador deberá"));
        Assert.Contains(quotes, quote => quote.Contains("cuenta propia deberá"));
        Assert.DoesNotContain(quotes, quote => quote.Contains("Información sobre los derechos"));
    }

    [Fact]
    public void MissingTitleUsesNeutralLabelsAndUnknownType()
    {
        var review = new ActionableSourceReviewBuilder().Build(new DocumentText(
            "Se mencionan ayudas y un formulario de solicitud.", "hash", "xml"), DateTimeOffset.UtcNow);
        Assert.Equal("general", review.Kind);
        Assert.Equal("Trámites mencionados", review.Groups.Single(group => group.Key == "application").Label);
    }

    [Fact]
    public void BdnsCallExtractWithoutAidInTitleUsesGrantTemplate()
    {
        var review = new ActionableSourceReviewBuilder().Build(new DocumentText(
            "BDNS (Identif.): 123456. Beneficiarios: empresas y profesionales autónomos.", "hash", "xml",
            OfficialTitle: "Extracto de la Orden de convocatoria de la Línea 2 del Programa Auto+"), DateTimeOffset.UtcNow);
        Assert.Equal("grant", review.Kind);
    }

    [Fact]
    public void ExtractedPassagesAreLiteralAndMissingFieldsRemainEmpty()
    {
        const string source = "Se convocan ayudas para pequeñas y medianas empresas. " +
            "Las personas beneficiarias deberán cumplir los requisitos indicados en la convocatoria. " +
            "El plazo de presentación de solicitudes se abrirá según se indique en la sede electrónica.";
        var review = new ActionableSourceReviewBuilder().Build(
            new DocumentText(source, "source-hash", "xml"), DateTimeOffset.UtcNow);

        Assert.Equal("source-hash", review.SourceHash);
        Assert.All(review.Groups.SelectMany(group => group.Quotes), quote =>
            Assert.Contains(quote, source, StringComparison.Ordinal));
        Assert.NotEmpty(review.Groups.Single(group => group.Key == "recipients").Quotes);
        Assert.NotEmpty(review.Groups.Single(group => group.Key == "deadlines").Quotes);
        Assert.Empty(review.Groups.Single(group => group.Key == "amount").Quotes);
    }

    [Fact]
    public void DoesNotInventADeadlineFromPublicationDate()
    {
        const string source = "Publicado el 26 de septiembre de 2026. El objeto es describir un programa.";
        var review = new ActionableSourceReviewBuilder().Build(
            new DocumentText(source, "source-hash", "xml"), DateTimeOffset.UtcNow);

        Assert.Empty(review.Groups.Single(group => group.Key == "deadlines").Quotes);
    }

    [Fact]
    public void HeadingIncludesFollowingParagraphWithoutSplittingDecimalAmount()
    {
        const string heading = "Importe de financiación convocado.";
        const string value = "La cuantía es de 240.000 euros para estas ayudas.";
        var text = $"{heading} {value}";
        var review = new ActionableSourceReviewBuilder().Build(
            new DocumentText(text, "source-hash", "xml", [heading, value]), DateTimeOffset.UtcNow);

        var amount = review.Groups.Single(group => group.Key == "amount");
        Assert.Contains(amount.Quotes, quote => quote.Contains("240.000 euros", StringComparison.Ordinal));
        Assert.All(amount.Quotes, quote => Assert.Contains(quote, text, StringComparison.Ordinal));
    }

    [Fact]
    public void AutonomousCommunitiesAreNotMistakenForSelfEmployedRecipients()
    {
        const string source = "Las comunidades autónomas han registrado superávits presupuestarios.";
        var review = new ActionableSourceReviewBuilder().Build(
            new DocumentText(source, "source-hash", "xml"), DateTimeOffset.UtcNow);

        Assert.Empty(review.Groups.Single(group => group.Key == "recipients").Quotes);
    }

    [Fact]
    public void KeepsAllThreeAutoPlusRecipientPassages()
    {
        const string companies = "1.º Podrán ser beneficiarios todo tipo de empresas con personalidad jurídica propia.";
        const string selfEmployed = "2.º Las personas físicas que desarrollen actividades económicas, por las que ofrezcan bienes o servicios en el mercado, deberán estar dadas de alta en el Censo de Empresarios, Profesionales y Retenedores.";
        const string excluded = "3.º No podrán ser beneficiarios los concesionarios o puntos de venta.";
        var text = $"{companies} {selfEmployed} {excluded}";
        var review = new ActionableSourceReviewBuilder().Build(
            new DocumentText(text, "source-hash", "xml", [companies, selfEmployed, excluded]),
            DateTimeOffset.UtcNow);

        var quotes = review.Groups.Single(group => group.Key == "recipients").Quotes;
        Assert.Equal(3, quotes.Count);
        Assert.Contains(quotes, quote => quote.Contains("todo tipo de empresas", StringComparison.Ordinal));
        Assert.Contains(quotes, quote => quote.Contains("personas físicas que desarrollen actividades económicas", StringComparison.Ordinal));
        Assert.Contains(quotes, quote => quote.Contains("No podrán ser beneficiarios los concesionarios", StringComparison.Ordinal));
        Assert.All(quotes, quote => Assert.Contains(quote, text, StringComparison.Ordinal));
    }

    [Fact]
    public void AmountPassageDoesNotBleedIntoFollowingDeadlineSection()
    {
        const string amount = "Cuantía convocada: 8.000.000,00 euros.";
        const string deadline = "Quinto. Plazo de presentación de solicitudes: 31 de diciembre de 2026.";
        var text = $"{amount} {deadline}";
        var review = new ActionableSourceReviewBuilder().Build(
            new DocumentText(text, "source-hash", "xml", [amount, deadline]), DateTimeOffset.UtcNow);

        var quote = Assert.Single(review.Groups.Single(group => group.Key == "amount").Quotes);
        Assert.Equal(amount, quote);
    }

    [Fact]
    public void FlattenedAmountAndDeadlineSectionsRemainSeparate()
    {
        const string source = "Cuantía convocada: 8.000.000,00 euros Quinto. Plazo de presentación de solicitudes: 31 de diciembre de 2026.";
        var review = new ActionableSourceReviewBuilder().Build(
            new DocumentText(source, "source-hash", "xml", [source]), DateTimeOffset.UtcNow);

        var amount = Assert.Single(review.Groups.Single(group => group.Key == "amount").Quotes);
        Assert.Equal("Cuantía convocada: 8.000.000,00 euros", amount);
        Assert.Contains("31 de diciembre", Assert.Single(review.Groups.Single(group => group.Key == "deadlines").Quotes));
    }

    [Fact]
    public void AdministrativeRegistryIsNotAnApplicationInstruction()
    {
        const string source = "El convenio se inscribirá en el Registro Electrónico Estatal de Órganos e Instrumentos de Cooperación.";
        var review = new ActionableSourceReviewBuilder().Build(
            new DocumentText(source, "source-hash", "xml"), DateTimeOffset.UtcNow);

        Assert.Empty(review.Groups.Single(group => group.Key == "application").Quotes);
    }

    [Fact]
    public void FindsActualSubmissionInstructions()
    {
        const string source = "Las solicitudes de participación serán presentadas por las entidades a través del formulario de solicitud.";
        var review = new ActionableSourceReviewBuilder().Build(
            new DocumentText(source, "source-hash", "xml"), DateTimeOffset.UtcNow);

        Assert.Equal(source, Assert.Single(review.Groups.Single(group => group.Key == "application").Quotes));
    }

    [Fact]
    public void KeepsSeparateBudgetRowsForCompaniesAndSelfEmployed()
    {
        const string total = "Cuantía total máxima: 50.000.000 euros.";
        const string companies = "Empresas privadas. Cuantía convocada: 42.000.000 euros.";
        const string selfEmployed = "Profesionales autónomos. Cuantía convocada: 8.000.000 euros.";
        var text = $"{total} {companies} {selfEmployed}";
        var review = new ActionableSourceReviewBuilder().Build(
            new DocumentText(text, "source-hash", "xml", [total, companies, selfEmployed]),
            DateTimeOffset.UtcNow);

        var quotes = review.Groups.Single(group => group.Key == "amount").Quotes;
        Assert.Equal(3, quotes.Count);
        Assert.Contains(quotes, quote => quote.Contains("8.000.000 euros", StringComparison.Ordinal));
    }
}
