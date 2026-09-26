using BoeRadar.Application;

namespace BoeRadar.UnitTests;

public sealed class ActionableSourceReviewTests
{
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
