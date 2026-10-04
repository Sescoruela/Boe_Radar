using BoeRadar.Sources;

namespace BoeRadar.SourceSpike.Tests;

public sealed class OfficialDocumentContentExtractorTests
{
    private readonly OfficialDocumentContentExtractor _sut = new();

    [Fact]
    public void ReferencesPreserveOfficialRelationAndDirectionWithoutChangingBodyHash()
    {
        const string xml = """
            <documento><analisis><referencias>
              <anteriores><anterior referencia="BOE-A-2003-20151"><palabra>MODIFICA</palabra><texto>el anexo de la Orden PRE/3026/2003</texto></anterior>
                <anterior referencia="DOUE-L-2023-81539"><palabra>DE CONFORMIDAD con</palabra><texto>el Reglamento de la Unión Europea</texto></anterior></anteriores>
              <posteriores><posterior referencia="BOE-A-2026-20001"><palabra>CORRECCIÓN de errores</palabra><texto>Corrección publicada posteriormente</texto></posterior></posteriores>
            </referencias></analisis><texto><p>Cuerpo original.</p></texto></documento>
            """;
        var references = _sut.ExtractReferences(xml);
        Assert.Equal(3, references.Count);
        Assert.Equal("MODIFICA", references[0].Relation);
        Assert.Equal("previous", references[0].Direction);
        Assert.Equal("subsequent", references[2].Direction);
        Assert.Equal("https://www.boe.es/buscar/doc.php?id=DOUE-L-2023-81539", references[1].OfficialUrl);
        Assert.Equal("Cuerpo original.", _sut.Extract(xml, "xml"));
    }

    [Theory]
    [InlineData("https://evil.example/BOE-A-2026-1")]
    [InlineData("BOE-A-2026-1&amp;redirect=evil")]
    [InlineData("BOE-A-2026-1/anything")]
    [InlineData("BOE-A-2026-1&#10;")]
    public void InvalidReferenceIdsCannotCreateOutboundLinks(string id)
    {
        Assert.Empty(_sut.ExtractReferences($"<documento><analisis><referencias><anteriores>" +
            $"<anterior referencia='{id}'><texto>Referencia</texto></anterior>" +
            "</anteriores></referencias></analisis></documento>"));
    }

    [Fact]
    public void ReferencesAreDeduplicatedBoundedAndDoNotComeFromBodyOrUnrelatedMetadata()
    {
        var entries = string.Concat(Enumerable.Range(1, 35).Select(index =>
            $"<anterior referencia='BOE-A-2026-{index}'><palabra>CITA</palabra><texto>Norma {index}</texto></anterior>"));
        var xml = $"<documento><analisis><referencias><anteriores>{entries}{entries}</anteriores>" +
            "</referencias></analisis><texto><anterior referencia='BOE-A-2026-999'/></texto></documento>";
        var references = _sut.ExtractReferences(xml);
        Assert.Equal(30, references.Count);
        Assert.Equal(30, references.Select(reference => reference.ExternalId).Distinct().Count());
        Assert.DoesNotContain(references, reference => reference.ExternalId.EndsWith("999"));
        Assert.Empty(_sut.ExtractReferences("<documento><texto><p>Orden relacionada sin enlace.</p></texto></documento>"));
    }

    [Fact]
    public void MalformedReferenceXmlReturnsSourceFormatError() =>
        Assert.Throws<BoeSourceFormatException>(() => _sut.ExtractReferences("<documento>"));

    [Fact]
    public void ExtractTitleReadsOfficialMetadataOnly()
    {
        const string xml = "<documento><metadatos><titulo> Acuerdo de Seguridad Social </titulo></metadatos>" +
            "<texto><titulo>Convocatoria de ayudas</titulo><p>Texto.</p></texto></documento>";
        Assert.Equal("Acuerdo de Seguridad Social", _sut.ExtractTitle(xml));
        Assert.Null(_sut.ExtractTitle("<documento><texto><titulo>Ayudas</titulo></texto></documento>"));
    }

    [Fact]
    public void ExtractTitleReadsRealBoeXmlWithoutChangingBodyHash()
    {
        var raw = Fixture.Read("boe-document-BOE-A-2024-10761.xml");
        var text = _sut.Extract(raw, "xml");
        var hash = OfficialDocumentContentExtractor.ComputeSha256(text);
        Assert.StartsWith("Modificaciones de los anexos", _sut.ExtractTitle(raw));
        Assert.Equal(hash, OfficialDocumentContentExtractor.ComputeSha256(_sut.Extract(raw, "xml")));
    }

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
