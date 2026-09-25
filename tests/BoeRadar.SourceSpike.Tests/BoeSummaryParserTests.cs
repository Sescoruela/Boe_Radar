using BoeRadar.Sources;

namespace BoeRadar.SourceSpike.Tests;

public sealed class BoeSummaryParserTests
{
    private readonly BoeSummaryParser _sut = new();

    [Theory]
    [InlineData("boe-summary-20240529.json", 2024, 5, 29)]
    [InlineData("boe-summary-20240601.json", 2024, 6, 1)]
    [InlineData("boe-summary-20240930.json", 2024, 9, 30)]
    public void Parse_RealSummary_NormalizesDocuments(
        string fixtureName,
        int year,
        int month,
        int day)
    {
        var result = _sut.Parse(Fixture.Read(fixtureName));

        Assert.Equal(new DateOnly(year, month, day), result.PublicationDate);
        Assert.True(result.Documents.Count > 10);
        Assert.Equal(
            result.Documents.Count,
            result.Documents.Select(item => item.Identifier).Distinct().Count());
        Assert.All(result.Documents, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Title));
            Assert.False(string.IsNullOrWhiteSpace(item.SectionName));
            Assert.False(string.IsNullOrWhiteSpace(item.DepartmentName));
            Assert.Equal(result.PublicationDate, item.PublicationDate);
            Assert.NotNull(item.OfficialPdfUrl);
        });
    }

    [Fact]
    public void Parse_SingleObjectNodes_AcceptsBoeShape()
    {
        const string json = """
            {
              "status": { "code": "200", "text": "ok" },
              "data": {
                "sumario": {
                  "metadatos": { "fecha_publicacion": "20240102" },
                  "diario": {
                    "numero": "1",
                    "seccion": {
                      "codigo": "3",
                      "nombre": "III. Otras disposiciones",
                      "departamento": {
                        "codigo": "1000",
                        "nombre": "ORGANISMO DE PRUEBA",
                        "epigrafe": {
                          "nombre": "Ayudas",
                          "item": {
                            "identificador": "BOE-A-2024-1",
                            "control": "2024/1",
                            "titulo": "Convocatoria de ayudas.",
                            "url_html": "https://www.boe.es/example.html",
                            "url_xml": "https://www.boe.es/example.xml",
                            "url_pdf": { "texto": "https://www.boe.es/example.pdf" }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """;

        var result = _sut.Parse(json);

        var item = Assert.Single(result.Documents);
        Assert.Equal("BOE-A-2024-1", item.Identifier);
        Assert.Equal("Ayudas", item.Epigraph);
        Assert.Equal("https://www.boe.es/example.pdf", item.OfficialPdfUrl?.AbsoluteUri);
    }

    [Fact]
    public void Parse_InvalidStatus_ExplainsSourceError()
    {
        const string json = """
            { "status": { "code": "404", "text": "no encontrado" } }
            """;

        var exception = Assert.Throws<BoeSourceFormatException>(() => _sut.Parse(json));

        Assert.Contains("404", exception.Message, StringComparison.Ordinal);
        Assert.Contains("no encontrado", exception.Message, StringComparison.Ordinal);
    }
}
