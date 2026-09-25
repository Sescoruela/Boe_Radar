using System.Net;
using BoeRadar.Sources;

namespace BoeRadar.SourceSpike.Tests;

public sealed class BoeOpenDataClientTests
{
    [Fact]
    public async Task GetSummaryAsync_UsesOfficialDateEndpointAndJsonAcceptHeader()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Fixture.Read("boe-summary-20240529.json"))
            };
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = BoeOpenDataClient.DefaultBaseAddress
        };
        var sut = new BoeOpenDataClient(
            httpClient,
            new BoeSummaryParser(),
            new OfficialDocumentContentExtractor());

        var result = await sut.GetSummaryAsync(new DateOnly(2024, 5, 29));

        Assert.NotEmpty(result.Documents);
        Assert.Equal(
            "https://www.boe.es/datosabiertos/api/boe/sumario/20240529",
            capturedRequest?.RequestUri?.AbsoluteUri);
        Assert.Contains(
            capturedRequest!.Headers.Accept,
            header => header.MediaType == "application/json");
    }

    [Fact]
    public async Task GetContentAsync_PrefersXmlAndCalculatesHash()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Fixture.Read("boe-document-BOE-A-2024-10761.xml"))
            };
        });
        using var httpClient = new HttpClient(handler);
        var sut = new BoeOpenDataClient(
            httpClient,
            new BoeSummaryParser(),
            new OfficialDocumentContentExtractor());
        var item = new BoeDocumentItem(
            "BOE-A-2024-10761",
            new DateOnly(2024, 5, 29),
            "130",
            "1",
            "I. Disposiciones generales",
            "9562",
            "Ministerio",
            null,
            "Documento",
            null,
            new Uri("https://www.boe.es/fallback.html"),
            new Uri("https://www.boe.es/preferred.xml"),
            new Uri("https://www.boe.es/document.pdf"));

        var result = await sut.GetContentAsync(item);

        Assert.Equal("https://www.boe.es/preferred.xml", capturedRequest?.RequestUri?.AbsoluteUri);
        Assert.Equal("xml", result.Format);
        Assert.Equal(64, result.Sha256.Length);
        Assert.NotEmpty(result.NormalizedText);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}
