using BoeRadar.Application;
using BoeRadar.Sources;

namespace BoeRadar.Infrastructure.Sources;

internal sealed class BoeOfficialGazetteSource(BoeOpenDataClient client)
    : IOfficialGazetteSource
{
    public string SourceCode => "BOE";

    public async Task<OfficialIssue> GetIssueAsync(
        DateOnly publicationDate,
        CancellationToken cancellationToken = default)
    {
        var result = await client.GetSummaryAsync(publicationDate, cancellationToken);
        var summaryUrl = new Uri(
            BoeOpenDataClient.DefaultBaseAddress,
            $"datosabiertos/api/boe/sumario/{publicationDate:yyyyMMdd}");

        return new OfficialIssue(
            SourceCode,
            result.PublicationDate,
            summaryUrl,
            result.Documents
                .Select(item => new OfficialDocument(
                    item.Identifier,
                    item.IssueNumber,
                    item.SectionCode,
                    item.SectionName,
                    item.DepartmentCode,
                    item.DepartmentName,
                    item.Epigraph,
                    item.Title,
                    item.ControlNumber,
                    item.OfficialHtmlUrl,
                    item.OfficialXmlUrl,
                    item.OfficialPdfUrl))
                .ToArray());
    }
}

