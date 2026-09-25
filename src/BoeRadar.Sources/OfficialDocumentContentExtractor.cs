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

