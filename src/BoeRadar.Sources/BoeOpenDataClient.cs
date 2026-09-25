using System.Globalization;
using System.Net;
using System.Net.Http.Headers;

namespace BoeRadar.Sources;

public sealed class BoeOpenDataClient
{
    public static readonly Uri DefaultBaseAddress = new("https://www.boe.es/");

    private readonly HttpClient _httpClient;
    private readonly BoeSummaryParser _summaryParser;
    private readonly OfficialDocumentContentExtractor _contentExtractor;
    private readonly TimeProvider _timeProvider;

    public BoeOpenDataClient(
        HttpClient httpClient,
        BoeSummaryParser summaryParser,
        OfficialDocumentContentExtractor contentExtractor,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _summaryParser = summaryParser ?? throw new ArgumentNullException(nameof(summaryParser));
        _contentExtractor = contentExtractor ?? throw new ArgumentNullException(nameof(contentExtractor));
        _timeProvider = timeProvider ?? TimeProvider.System;

        _httpClient.BaseAddress ??= DefaultBaseAddress;
    }

    public async Task<BoeIssueSummary> GetSummaryAsync(
        DateOnly publicationDate,
        CancellationToken cancellationToken = default)
    {
        var path = $"datosabiertos/api/boe/sumario/{publicationDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        return _summaryParser.Parse(payload);
    }

    public async Task<OfficialDocumentContent> GetContentAsync(
        BoeDocumentItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var (uri, format, mediaType) = item.OfficialXmlUrl is not null
            ? (item.OfficialXmlUrl, "xml", "application/xml")
            : item.OfficialHtmlUrl is not null
                ? (item.OfficialHtmlUrl, "html", "text/html")
                : throw new BoeSourceFormatException(
                    $"El documento {item.Identifier} no ofrece URL XML ni HTML.");

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType));

        using var response = await SendAsync(request, cancellationToken);
        var rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var normalizedText = _contentExtractor.Extract(rawContent, format);

        return new OfficialDocumentContent(
            format,
            normalizedText,
            OfficialDocumentContentExtractor.ComputeSha256(normalizedText),
            _timeProvider.GetUtcNow());
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new BoeSourceUnavailableException(
                $"No se pudo acceder a {request.RequestUri}.",
                exception);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            response.Dispose();
            throw new BoeSourceUnavailableException(
                $"El BOE no dispone de información para {request.RequestUri}.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;
            var reason = response.ReasonPhrase;
            response.Dispose();
            throw new BoeSourceUnavailableException(
                $"El BOE respondió HTTP {statusCode} ({reason}) para {request.RequestUri}.");
        }

        return response;
    }
}

