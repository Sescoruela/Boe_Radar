using System.Text.RegularExpressions;

namespace BoeRadar.Application;

public sealed record SourceFactGroup(string Key, string Label, IReadOnlyList<string> Quotes);

public sealed record ActionableSourceReview(
    string SourceHash,
    DateTimeOffset ReviewedAt,
    IReadOnlyList<SourceFactGroup> Groups)
{
    public string Kind { get; init; } = "general";
    public string KindLabel { get; init; } = "Publicación por revisar";
    public string NextStep { get; init; } = "Comprueba el ámbito y el efecto de esta publicación en la fuente oficial.";
    public SourceProfileContrast? ProfileContrast { get; init; }
    public IReadOnlyList<DocumentReference> References { get; init; } = [];
}

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

    private static readonly (string Key, string Label, Regex Pattern)[] CommonPatterns =
    [
        Create("affected", "A quién puede afectar", @"\b(empresari[oa]s?|empresas?|trabajadores?|empleadores?|profesionales?|sujetos obligados|contribuyentes?|personas f[ií]sicas)\b"),
        Create("obligations", "Actuaciones u obligaciones mencionadas", @"\b(deberán|deberá|deben|obligad[oa]s?|presentar|comunicar|cotizar|retener)\b"),
        Create("effective", "Entrada en vigor o efectos", @"\b(entrar[áa] en vigor|entrada en vigor|surtir[áa] efectos|producir[áa] efectos|ser[áa] aplicable|se aplicar[áa]|con efectos (?:desde|a partir))\b"),
        Create("transitional", "Excepciones y régimen transitorio", @"\b(excepciones?|excluid[oa]s?|no (?:ser[áa]n|podrán)|disposici[oó]n transitoria|r[eé]gimen transitorio)\b"),
    ];

    private static readonly (string Key, string Label, Regex Pattern) Changes = Create(
        "changes", "Cambios normativos mencionados", @"\b(modifica|modifican|modificaci[oó]n|deroga|anula|anulaci[oó]n|se establece|se aprueba|queda redactado|precios de venta)\b");
    private static readonly (string Key, string Label, Regex Pattern) Taxes = Create(
        "taxes", "Impuestos o conceptos fiscales mencionados", @"\b(IVA|IRPF|impuestos?|tributari[oa]s?|retenciones?|base imponible|deducciones?)\b");

    // Only source metadata selects the template. Mentions of aid or taxes in
    // an unrelated article do not turn the whole document into a call or tax rule.
    private static string Classify(DocumentText content)
    {
        var title = content.OfficialTitle;
        if (string.IsNullOrWhiteSpace(title)) return "general";
        if (Regex.IsMatch(title, @"\b(convocan|convocatoria|bases reguladoras)\b", RegexOptions.IgnoreCase,
                TimeSpan.FromMilliseconds(250)) &&
            (Regex.IsMatch(title, @"\b(ayudas?|subvenciones?)\b", RegexOptions.IgnoreCase,
                TimeSpan.FromMilliseconds(250)) ||
             (title.StartsWith("Extracto de", StringComparison.OrdinalIgnoreCase) &&
              Regex.IsMatch(content.Text, @"\bBDNS\b", RegexOptions.IgnoreCase,
                  TimeSpan.FromMilliseconds(250))))) return "grant";
        if (Taxes.Pattern.IsMatch(title) || Regex.IsMatch(title, @"\b(fiscal|fiscales)\b",
                RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250))) return "tax";
        if (Regex.IsMatch(title, @"\b(ley|decreto|reglamento|normas|modifica|sentencia|acuerdo administrativo|precios de venta|seguridad social)\b",
                RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250))) return "regulation";
        return "general";
    }

    public ActionableSourceReview Build(DocumentText content, DateTimeOffset reviewedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content.Text);
        var kind = Classify(content);
        IEnumerable<(string Key, string Label, Regex Pattern)> selectedPatterns = kind switch
        {
            "grant" => Patterns.Concat(CommonPatterns.Where(pattern => pattern.Key is "effective" or "transitional")),
            "tax" => CommonPatterns.Prepend(Patterns[1]).Prepend(Taxes)
                .Append(Changes).Append(Create("deadlines", "Plazos de cumplimiento", @"\b(plazos?|fecha l[ií]mite|d[ií]as h[aá]biles|hasta el d[ií]a)\b")),
            "regulation" => CommonPatterns.Prepend(Patterns[1]).Prepend(Changes),
            _ => Patterns.Select(pattern => (pattern.Key, Label: pattern.Key switch
                {
                    "recipients" => "Personas o entidades mencionadas",
                    "requirements" => "Condiciones mencionadas",
                    "amount" => "Importes mencionados",
                    "deadlines" => "Plazos mencionados",
                    "application" => "Trámites mencionados",
                    _ => pattern.Label
                }, pattern.Pattern)).Concat(CommonPatterns.Where(pattern => pattern.Key == "effective"))
        };
        var groups = selectedPatterns.Select(pattern => new SourceFactGroup(
            pattern.Key,
            pattern.Label,
            FindQuotes(content, pattern.Pattern,
                pattern.Key is "amount" or "recipients" ? 3 : MaximumQuotesPerGroup))).ToArray();
        return new ActionableSourceReview(content.Sha256, reviewedAt, groups)
        {
            Kind = kind,
            References = content.References ?? [],
            KindLabel = kind switch
            {
                "grant" => "Ayudas y subvenciones",
                "tax" => "Fiscalidad",
                "regulation" => "Cambio normativo o condiciones de actividad",
                _ => "Publicación por revisar"
            },
            NextStep = kind switch
            {
                "grant" => "Localiza las bases y la convocatoria completa. Comprueba destinatarios, exclusiones y plazo antes de preparar una solicitud; publicar las bases no implica que el plazo esté abierto.",
                "tax" => "Comprueba el impuesto, los contribuyentes afectados, las fechas de efectos y los trámites. Contrasta con tu asesoría qué actuación corresponde a tu negocio.",
                "regulation" => "Revisa qué disposición cambia, a quién afecta y cuándo produce efectos. Comprueba las obligaciones y las excepciones antes de actuar.",
                _ => "La plantilla no permite determinar el tipo de publicación. Abre la fuente y comprueba su ámbito, condiciones y posibles actuaciones."
            }
        };
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

            // Preserve conditional context when the entire paragraph fits.
            var start = passage.Length <= MaximumQuoteLength ? 0 : Math.Max(0, match.Index - 100);
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
