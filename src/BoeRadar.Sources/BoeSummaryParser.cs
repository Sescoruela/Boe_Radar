using System.Globalization;
using System.Text.Json;

namespace BoeRadar.Sources;

public sealed class BoeSummaryParser
{
    private const string SourceDateFormat = "yyyyMMdd";

    public BoeIssueSummary Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var status = RequiredProperty(root, "status");
            var statusCode = ReadString(status, "code");

            if (statusCode != "200")
            {
                var statusText = ReadString(status, "text") ?? "Respuesta desconocida";
                throw new BoeSourceFormatException(
                    $"El BOE devolvió estado {statusCode ?? "sin código"}: {statusText}.");
            }

            var data = RequiredProperty(root, "data");
            var summary = RequiredProperty(data, "sumario");
            var metadata = RequiredProperty(summary, "metadatos");
            var sourceDate = ReadRequiredString(metadata, "fecha_publicacion");

            if (!DateOnly.TryParseExact(
                    sourceDate,
                    SourceDateFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var publicationDate))
            {
                throw new BoeSourceFormatException(
                    $"La fecha de publicación '{sourceDate}' no usa el formato {SourceDateFormat}.");
            }

            var items = new Dictionary<string, BoeDocumentItem>(StringComparer.OrdinalIgnoreCase);

            foreach (var issue in ReadOneOrMany(summary, "diario"))
            {
                var issueNumber = ReadRequiredString(issue, "numero");

                foreach (var section in ReadOneOrMany(issue, "seccion"))
                {
                    var sectionCode = ReadRequiredString(section, "codigo");
                    var sectionName = ReadRequiredString(section, "nombre");

                    foreach (var department in ReadOneOrMany(section, "departamento"))
                    {
                        var departmentCode = ReadRequiredString(department, "codigo");
                        var departmentName = ReadRequiredString(department, "nombre");

                        AddItems(
                            department,
                            epigraph: null,
                            publicationDate,
                            issueNumber,
                            sectionCode,
                            sectionName,
                            departmentCode,
                            departmentName,
                            items);

                        foreach (var epigraph in ReadOneOrMany(department, "epigrafe"))
                        {
                            AddItems(
                                epigraph,
                                ReadString(epigraph, "nombre"),
                                publicationDate,
                                issueNumber,
                                sectionCode,
                                sectionName,
                                departmentCode,
                                departmentName,
                                items);
                        }
                    }
                }
            }

            if (items.Count == 0)
            {
                throw new BoeSourceFormatException(
                    "El sumario es válido, pero no contiene documentos reconocibles.");
            }

            return new BoeIssueSummary(publicationDate, items.Values.ToArray());
        }
        catch (JsonException exception)
        {
            throw new BoeSourceFormatException("La respuesta del BOE no es JSON válido.", exception);
        }
    }

    private static void AddItems(
        JsonElement container,
        string? epigraph,
        DateOnly publicationDate,
        string issueNumber,
        string sectionCode,
        string sectionName,
        string departmentCode,
        string departmentName,
        IDictionary<string, BoeDocumentItem> destination)
    {
        foreach (var item in ReadOneOrMany(container, "item"))
        {
            var identifier = ReadRequiredString(item, "identificador");
            var normalized = new BoeDocumentItem(
                identifier,
                publicationDate,
                issueNumber,
                sectionCode,
                sectionName,
                departmentCode,
                departmentName,
                epigraph,
                ReadRequiredString(item, "titulo"),
                ReadString(item, "control"),
                ReadUri(item, "url_html"),
                ReadUri(item, "url_xml"),
                ReadUri(item, "url_pdf"));

            destination.TryAdd(identifier, normalized);
        }
    }

    private static IEnumerable<JsonElement> ReadOneOrMany(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            yield break;
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in value.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    yield return element;
                }
            }

            yield break;
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            yield return value;
            yield break;
        }

        throw new BoeSourceFormatException(
            $"El campo '{propertyName}' debe ser un objeto o un array.");
    }

    private static JsonElement RequiredProperty(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
        {
            throw new BoeSourceFormatException($"Falta el campo obligatorio '{propertyName}'.");
        }

        return value;
    }

    private static string ReadRequiredString(JsonElement parent, string propertyName) =>
        ReadString(parent, propertyName) is { Length: > 0 } value
            ? value
            : throw new BoeSourceFormatException(
                $"Falta el valor obligatorio '{propertyName}'.");

    private static string? ReadString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static Uri? ReadUri(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        var rawUrl = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Object => ReadString(value, "texto"),
            _ => null
        };

        return Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri) ? uri : null;
    }
}

