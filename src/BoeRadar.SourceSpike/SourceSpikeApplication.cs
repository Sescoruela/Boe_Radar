using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using BoeRadar.Sources;

namespace BoeRadar.SourceSpike;

public static class SourceSpikeApplication
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<int> RunAsync(string[] args)
    {
        if (!CliOptions.TryParse(args, out var options, out var error))
        {
            if (error is not null)
            {
                await Console.Error.WriteLineAsync(error);
            }

            await Console.Error.WriteLineAsync(CliOptions.Usage);
            return error is null ? 0 : 2;
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        using var httpClient = new HttpClient
        {
            BaseAddress = BoeOpenDataClient.DefaultBaseAddress,
            Timeout = TimeSpan.FromSeconds(60)
        };
        httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("BOE-Radar-IA", "0.1"));

        var client = new BoeOpenDataClient(
            httpClient,
            new BoeSummaryParser(),
            new OfficialDocumentContentExtractor());

        try
        {
            var summary = await client.GetSummaryAsync(options!.Date, cancellation.Token);
            var selectedDocuments = options.Limit is { } limit
                ? summary.Documents.Take(limit).ToArray()
                : summary.Documents;
            var projections = new List<object>(selectedDocuments.Count);

            foreach (var item in selectedDocuments)
            {
                OfficialDocumentContent? content = null;

                if (options.IncludeContent)
                {
                    content = await client.GetContentAsync(item, cancellation.Token);
                }

                projections.Add(new
                {
                    item.Identifier,
                    item.PublicationDate,
                    item.IssueNumber,
                    section = new { item.SectionCode, item.SectionName },
                    department = new { item.DepartmentCode, item.DepartmentName },
                    item.Epigraph,
                    item.Title,
                    item.ControlNumber,
                    item.OfficialHtmlUrl,
                    item.OfficialXmlUrl,
                    item.OfficialPdfUrl,
                    content = content is null
                        ? null
                        : new
                        {
                            content.Format,
                            content.Sha256,
                            characterCount = content.NormalizedText.Length,
                            content.FetchedAt,
                            content.NormalizedText
                        }
                });
            }

            var output = new
            {
                source = "BOE",
                summary.PublicationDate,
                totalDocuments = summary.Documents.Count,
                returnedDocuments = projections.Count,
                documents = projections
            };
            var json = JsonSerializer.Serialize(output, JsonOptions);

            if (options.OutputPath is null)
            {
                await Console.Out.WriteLineAsync(json);
            }
            else
            {
                var fullPath = Path.GetFullPath(options.OutputPath);
                var directory = Path.GetDirectoryName(fullPath);

                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(fullPath, json, cancellation.Token);
                await Console.Error.WriteLineAsync($"Resultado escrito en {fullPath}");
            }

            return 0;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            await Console.Error.WriteLineAsync("Operación cancelada.");
            return 130;
        }
        catch (Exception exception) when (
            exception is BoeSourceFormatException or BoeSourceUnavailableException)
        {
            await Console.Error.WriteLineAsync(exception.Message);
            return 1;
        }
    }

    private sealed record CliOptions(
        DateOnly Date,
        int? Limit,
        bool IncludeContent,
        string? OutputPath)
    {
        public const string Usage = """
            Uso:
              dotnet run --project src/BoeRadar.SourceSpike -- --date AAAA-MM-DD [opciones]

            Opciones:
              --limit N             Devuelve solo los primeros N documentos.
              --include-content     Descarga XML/HTML, normaliza el texto y calcula SHA-256.
              --output RUTA         Escribe el JSON en un archivo en lugar de stdout.
              --help                Muestra esta ayuda.

            Ejemplo:
              dotnet run --project src/BoeRadar.SourceSpike -- --date 2024-05-29 --limit 1 --include-content
            """;

        public static bool TryParse(
            IReadOnlyList<string> args,
            out CliOptions? options,
            out string? error)
        {
            options = null;
            error = null;
            DateOnly? date = null;
            int? limit = null;
            var includeContent = false;
            string? outputPath = null;

            for (var index = 0; index < args.Count; index++)
            {
                var argument = args[index];

                switch (argument)
                {
                    case "--help" or "-h":
                        return false;

                    case "--include-content":
                        includeContent = true;
                        break;

                    case "--date":
                        if (!TryReadValue(args, ref index, argument, out var rawDate, out error))
                        {
                            return false;
                        }

                        if (!DateOnly.TryParseExact(
                                rawDate,
                                "yyyy-MM-dd",
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.None,
                                out var parsedDate))
                        {
                            error = "--date debe usar el formato AAAA-MM-DD.";
                            return false;
                        }

                        date = parsedDate;
                        break;

                    case "--limit":
                        if (!TryReadValue(args, ref index, argument, out var rawLimit, out error))
                        {
                            return false;
                        }

                        if (!int.TryParse(rawLimit, CultureInfo.InvariantCulture, out var parsedLimit) ||
                            parsedLimit < 1)
                        {
                            error = "--limit debe ser un entero mayor que cero.";
                            return false;
                        }

                        limit = parsedLimit;
                        break;

                    case "--output":
                        if (!TryReadValue(args, ref index, argument, out outputPath, out error))
                        {
                            return false;
                        }

                        break;

                    default:
                        error = $"Argumento desconocido: {argument}";
                        return false;
                }
            }

            if (date is null)
            {
                error = "Falta el argumento obligatorio --date.";
                return false;
            }

            if (includeContent && limit is null)
            {
                error = "--include-content requiere --limit para evitar descargas masivas accidentales.";
                return false;
            }

            options = new CliOptions(date.Value, limit, includeContent, outputPath);
            return true;
        }

        private static bool TryReadValue(
            IReadOnlyList<string> args,
            ref int index,
            string argument,
            out string value,
            out string? error)
        {
            if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = string.Empty;
                error = $"Falta el valor de {argument}.";
                return false;
            }

            value = args[++index];
            error = null;
            return true;
        }
    }
}
