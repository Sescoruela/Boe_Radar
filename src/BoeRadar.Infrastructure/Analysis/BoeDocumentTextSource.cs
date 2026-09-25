using System.Net.Http.Headers;
using BoeRadar.Application;
using BoeRadar.Sources;

namespace BoeRadar.Infrastructure.Analysis;

internal sealed class BoeDocumentTextSource(
    HttpClient httpClient,
    OfficialDocumentContentExtractor extractor)
    : IOfficialDocumentTextSource
{
    public async Task<DocumentText> GetAsync(
        Uri officialXmlUrl,
        CancellationToken cancellationToken = default)
    {
        if (!officialXmlUrl.IsAbsoluteUri ||
            !officialXmlUrl.Host.Equals("www.boe.es", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Solo se permite descargar contenido desde www.boe.es.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, officialXmlUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        var text = extractor.Extract(raw, "xml");
        return new DocumentText(text, OfficialDocumentContentExtractor.ComputeSha256(text), "xml");
    }
}
