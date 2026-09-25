using BoeRadar.Domain;

namespace BoeRadar.UnitTests;

public sealed class SourceDocumentTests
{
    [Fact]
    public void Update_WithIdenticalData_DoesNotChangeTimestamp()
    {
        var createdAt = new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero);
        var document = CreateDocument(createdAt);

        var changed = document.Update(
            document.IssueId,
            document.Title,
            document.DepartmentCode,
            document.Department,
            document.SectionCode,
            document.SectionName,
            document.Epigraph,
            document.ControlNumber,
            document.OfficialHtmlUrl,
            document.OfficialXmlUrl,
            document.OfficialPdfUrl,
            createdAt.AddHours(1));

        Assert.False(changed);
        Assert.Equal(createdAt, document.UpdatedAt);
    }

    [Fact]
    public void Update_WithChangedTitle_UpdatesEntityAndTimestamp()
    {
        var createdAt = new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero);
        var updatedAt = createdAt.AddHours(1);
        var document = CreateDocument(createdAt);

        var changed = document.Update(
            document.IssueId,
            "Título corregido",
            document.DepartmentCode,
            document.Department,
            document.SectionCode,
            document.SectionName,
            document.Epigraph,
            document.ControlNumber,
            document.OfficialHtmlUrl,
            document.OfficialXmlUrl,
            document.OfficialPdfUrl,
            updatedAt);

        Assert.True(changed);
        Assert.Equal("Título corregido", document.Title);
        Assert.Equal(updatedAt, document.UpdatedAt);
    }

    private static SourceDocument CreateDocument(DateTimeOffset now) =>
        SourceDocument.Create(
            Guid.CreateVersion7(),
            GazetteSource.BoeId,
            "BOE-A-2026-1",
            new DateOnly(2026, 9, 22),
            "Título original",
            "1000",
            "Ministerio de prueba",
            "3",
            "III. Otras disposiciones",
            "Ayudas",
            "2026/1",
            "https://www.boe.es/example.html",
            "https://www.boe.es/example.xml",
            "https://www.boe.es/example.pdf",
            now);
}

