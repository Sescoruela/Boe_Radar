using BoeRadar.Sources;

namespace BoeRadar.SourceSpike.Tests;

public sealed class OfficialDocumentContentExtractorTests
{
    private readonly OfficialDocumentContentExtractor _sut = new();

    [Fact]
    public void Extract_RealBoeXml_ReturnsOnlyCanonicalDocumentText()
    {
        var result = _sut.Extract(Fixture.Read("boe-document-BOE-A-2024-10761.xml"), "xml");

        Assert.Contains("Mar Mediterráneo", result, StringComparison.Ordinal);
        Assert.DoesNotContain("fecha_actualizacion", result, StringComparison.Ordinal);
        Assert.DoesNotContain("metadata-eli", result, StringComparison.Ordinal);
        Assert.DoesNotContain("  ", result, StringComparison.Ordinal);
        Assert.True(result.Length > 1_000);
    }

    [Fact]
    public void Extract_Html_RemovesMarkupScriptsAndDecodesEntities()
    {
        const string html = """
            <html><head><style>.x { color: red; }</style></head>
            <body><h1>Ayudas &amp; subvenciones</h1><script>alert('x')</script><p>Plazo de 20 días.</p></body></html>
            """;

        var result = _sut.Extract(html, "html");

        Assert.Equal("Ayudas & subvenciones Plazo de 20 días.", result);
    }

    [Fact]
    public void ComputeSha256_SameCanonicalText_IsDeterministic()
    {
        const string content = "Texto oficial canónico.";

        var first = OfficialDocumentContentExtractor.ComputeSha256(content);
        var second = OfficialDocumentContentExtractor.ComputeSha256(content);

        Assert.Equal(first, second);
        Assert.Matches("^[a-f0-9]{64}$", first);
    }

    [Fact]
    public void ExtractPassages_PreservesAmountsAndParagraphBoundaries()
    {
        const string xml = "<documento><texto><p>Importe de financiación convocado.</p>" +
            "<p>La cuantía es de 240.000 euros.</p></texto></documento>";

        var passages = _sut.ExtractPassages(xml);
        var fullText = _sut.Extract(xml, "xml");

        Assert.Equal(["Importe de financiación convocado.", "La cuantía es de 240.000 euros."], passages);
        Assert.All(passages, passage => Assert.Contains(passage, fullText, StringComparison.Ordinal));
    }
}
