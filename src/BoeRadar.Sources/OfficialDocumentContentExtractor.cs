using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace BoeRadar.Sources;

public sealed partial class OfficialDocumentContentExtractor
{
    private static readonly HashSet<string> XmlContentElementNames = new(
        ["texto", "texto_original"],
        StringComparer.OrdinalIgnoreCase);

    public string Extract(string rawContent, string format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawContent);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        var extracted = format.ToLowerInvariant() switch
        {
            "xml" => ExtractXml(rawContent),
            "html" => ExtractHtml(rawContent),
            _ => throw new ArgumentOutOfRangeException(
                nameof(format),
                format,
                "Solo se admiten los formatos xml y html.")
        };

        var normalized = Normalize(extracted);

        return normalized.Length > 0
            ? normalized
            : throw new BoeSourceFormatException(
                $"El documento {format} no contiene texto oficial reconocible.");
    }

    public static string ComputeSha256(string normalizedText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedText);
        var bytes = Encoding.UTF8.GetBytes(normalizedText);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    public IReadOnlyList<string> ExtractPassages(string rawContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawContent);
        try
        {
            var document = XDocument.Parse(rawContent, LoadOptions.PreserveWhitespace);
            var contentElement = document.Root?.Elements()
                .FirstOrDefault(element => XmlContentElementNames.Contains(element.Name.LocalName));
            if (contentElement is null)
                throw new BoeSourceFormatException("El XML del BOE no contiene un nodo de texto conocido.");

            return contentElement.Descendants()
                .Where(element => element.Name.LocalName.Equals("p", StringComparison.OrdinalIgnoreCase))
                .Select(element => Normalize(string.Join(" ", element.DescendantNodes().OfType<XText>()
                    .Select(node => node.Value))))
                .Where(value => value.Length > 0)
                .ToArray();
        }
        catch (System.Xml.XmlException exception)
        {
            throw new BoeSourceFormatException("El documento del BOE no es XML válido.", exception);
        }
    }

    public string? ExtractTitle(string rawContent)
    {
        try
        {
            var document = XDocument.Parse(rawContent);
            var title = document.Root?.Elements()
                .FirstOrDefault(element => element.Name.LocalName == "metadatos")?
                .Elements().FirstOrDefault(element => element.Name.LocalName == "titulo")?.Value;
            return string.IsNullOrWhiteSpace(title) ? null : Normalize(title);
        }
        catch (System.Xml.XmlException exception)
        {
            throw new BoeSourceFormatException("El documento del BOE no es XML válido.", exception);
        }
    }

    public IReadOnlyList<OfficialDocumentReference> ExtractReferences(string rawContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawContent);
        try
        {
            var document = XDocument.Parse(rawContent);
            var analysis = document.Root?.Elements().FirstOrDefault(element => element.Name.LocalName == "analisis");
            var references = analysis?.Elements().FirstOrDefault(element => element.Name.LocalName == "referencias");
            var results = new List<OfficialDocumentReference>();
            foreach (var (container, entry, direction) in new[]
                { ("anteriores", "anterior", "previous"), ("posteriores", "posterior", "subsequent") })
            {
                var entries = references?.Elements().FirstOrDefault(element => element.Name.LocalName == container)?
                    .Elements().Where(element => element.Name.LocalName == entry) ?? [];
                foreach (var reference in entries)
                {
                    var id = reference.Attribute("referencia")?.Value;
                    if (id is null || !OfficialReferenceIdRegex().IsMatch(id)) continue;
                    var relation = reference.Elements().FirstOrDefault(element => element.Name.LocalName == "palabra")?.Value;
                    var description = reference.Elements().FirstOrDefault(element => element.Name.LocalName == "texto")?.Value;
                    results.Add(new(id, BoundedLabel(relation, 100, "Referencia oficial"),
                        BoundedLabel(description, 500, id), direction, $"https://www.boe.es/buscar/doc.php?id={id}"));
                }
            }
            return results.DistinctBy(reference => (reference.ExternalId, reference.Direction, reference.Relation))
                .Take(30).ToArray();
        }
        catch (System.Xml.XmlException exception)
        {
            throw new BoeSourceFormatException("El documento del BOE no es XML válido.", exception);
        }
    }

    private static string BoundedLabel(string? value, int maximumLength, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : Normalize(value);
        return normalized.Length <= maximumLength ? normalized : normalized[..maximumLength] + "…";
    }

    [GeneratedRegex(@"\A(?:BOE-[AB]|DOUE-L)-[0-9]{4}-[0-9]{1,8}\z", RegexOptions.CultureInvariant)]
    private static partial Regex OfficialReferenceIdRegex();

    private static string ExtractXml(string rawContent)
    {
        try
        {
            var document = XDocument.Parse(rawContent, LoadOptions.PreserveWhitespace);
            var contentElement = document.Root?
                .Elements()
                .FirstOrDefault(element =>
                    XmlContentElementNames.Contains(element.Name.LocalName));

            if (contentElement is null)
            {
                throw new BoeSourceFormatException(
                    "El XML del BOE no contiene un nodo de texto conocido.");
            }

            return string.Join(
                " ",
                contentElement
                    .DescendantNodesAndSelf()
                    .OfType<XText>()
                    .Select(node => node.Value));
        }
        catch (System.Xml.XmlException exception)
        {
            throw new BoeSourceFormatException("El documento del BOE no es XML válido.", exception);
        }
    }

    private static string ExtractHtml(string rawContent)
    {
        var withoutScripts = ScriptOrStyleRegex().Replace(rawContent, " ");
        var withoutTags = HtmlTagRegex().Replace(withoutScripts, " ");
        return WebUtility.HtmlDecode(withoutTags);
    }

    private static string Normalize(string value)
    {
        var withoutInvisibleCharacters = value
            .Replace("\u200B", string.Empty, StringComparison.Ordinal)
            .Replace("\uFEFF", string.Empty, StringComparison.Ordinal);

        return WhitespaceRegex()
            .Replace(withoutInvisibleCharacters, " ")
            .Trim()
            .Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex(@"<(script|style)\b[^>]*>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptOrStyleRegex();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
