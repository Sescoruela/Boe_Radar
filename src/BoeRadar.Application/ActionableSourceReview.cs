using System.Text.RegularExpressions;

namespace BoeRadar.Application;

public sealed record SourceFactGroup(string Key, string Label, IReadOnlyList<string> Quotes);

public sealed record ActionableSourceReview(
    string SourceHash,
    DateTimeOffset ReviewedAt,
    IReadOnlyList<SourceFactGroup> Groups);

/// <summary>
/// Locates passages for a human to check. It does not assert eligibility,
/// calculate deadlines, or turn a source passage into a verified fact.
/// </summary>
public sealed class ActionableSourceReviewBuilder
{
    private const int MaximumQuoteLength = 700;
    private const int MaximumQuotesPerGroup = 2;

    private static readonly (string Key, string Label, Regex Pattern)[] Patterns =
    [
        Create("recipients", "Destinatarios", @"\b(beneficiari[oa]s?|destinatari[oa]s?|pymes?|pequeñas y medianas empresas)\b|(?<!comunidades )(?<!comunidad )\baut[oó]nom[oa]s?\b|\bpersonas f[ií]sicas que (?:desarrollen|realicen|ejerzan) actividades econ[oó]micas\b"),
        Create("territory", "Ámbito territorial", @"\b(ámbito territorial|comunidad autónoma|municipios?|provincias?|territorio nacional)\b"),
        Create("requirements", "Requisitos", @"\b(requisitos?|deberán cumplir|deberán reunir|condiciones para acceder)\b"),
        Create("amount", "Cuantía o presupuesto", @"\b(cuant[ií]a|importe|euros?)\b|€"),
        Create("deadlines", "Plazos", @"\b(plazo de (presentación|solicitud)|fecha límite|días hábiles|hasta el día|finalizará el)\b"),
        Create("application", "Cómo presentar la solicitud", @"\b(solicitudes?(?: de participación)? (?:se )?(?:presentarán|presentarse|serán presentadas)|solicitantes? (?:deben|deberán) presentar (?:su|la) solicitud|solicitud(?:es)? electrónic[ao]s?|formulario de solicitud|sede electrónica.{0,100}solicitud|registro electrónico.{0,100}solicitud)\b")
    ];

    public ActionableSourceReview Build(DocumentText content, DateTimeOffset reviewedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content.Text);
        var groups = Patterns.Select(pattern => new SourceFactGroup(
            pattern.Key,
            pattern.Label,
            FindQuotes(content, pattern.Pattern,
                pattern.Key is "amount" or "recipients" ? 3 : MaximumQuotesPerGroup))).ToArray();
        return new ActionableSourceReview(content.Sha256, reviewedAt, groups);
    }

    private static IReadOnlyList<string> FindQuotes(DocumentText content, Regex pattern, int maximumQuotes)
    {
        var passages = content.Passages is { Count: > 0 }
            ? content.Passages
            : [content.Text];
        var quotes = new List<string>();
        for (var index = 0; index < passages.Count; index++)
        {
            var passage = passages[index];
            var match = pattern.Match(passage);
            if (!match.Success)
                continue;

            // In BOE extracts a heading is often its own paragraph, followed
            // by the amount, deadline, or application instructions.
            // Extend a short heading or introductory line, but not a line that
            // already contains an amount/date: that would bleed into the next
            // section of the announcement.
            if (passage.Length < 150 &&
                !Regex.IsMatch(passage, @"\b\d{2,}(?:[.,]\d+)*\b", RegexOptions.CultureInvariant) &&
                (passage.EndsWith(':') || passage.Length < 80))
            {
                for (var nextIndex = index + 1;
                     nextIndex < passages.Count && nextIndex <= index + 3;
                     nextIndex++)
                {
                    var withNext = $"{passage} {passages[nextIndex]}";
                    if (!content.Text.Contains(withNext, StringComparison.Ordinal))
                        break;
                    passage = withNext;
                    if (passages[nextIndex].Length >= 150 || passage.Length >= MaximumQuoteLength)
                        break;
                }
            }

            // BOE XML may flatten a table and the following numbered section
            // into one paragraph. Keep each quote inside its own section.
            var followingSection = Regex.Match(passage[match.Index..],
                @"\s(?:Primero|Segundo|Tercero|Cuarto|Quinto|Sexto|Séptimo|Octavo|Noveno|Décimo)\.\s",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(250));
            if (followingSection.Success)
                passage = passage[..(match.Index + followingSection.Index)].TrimEnd();

            var start = Math.Max(0, match.Index - 100);
            if (start > 0)
            {
                var wordStart = passage.IndexOf(' ', start);
                if (wordStart >= 0 && wordStart < match.Index)
                    start = wordStart + 1;
            }

            var end = Math.Min(passage.Length, start + MaximumQuoteLength);
            if (end < passage.Length)
            {
                var wordEnd = passage.LastIndexOf(' ', end);
                if (wordEnd > match.Index + match.Length)
                    end = wordEnd;
            }

            var quote = passage[start..end].Trim();
            if (!content.Text.Contains(quote, StringComparison.Ordinal) ||
                quotes.Any(existing => existing.Contains(quote, StringComparison.Ordinal) ||
                    quote.Contains(existing, StringComparison.Ordinal)))
            {
                continue;
            }

            quotes.Add(quote);
            if (quotes.Count == maximumQuotes)
            {
                break;
            }
        }

        return quotes;
    }

    private static (string, string, Regex) Create(string key, string label, string expression) =>
        (key, label, new Regex(expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(250)));
}
