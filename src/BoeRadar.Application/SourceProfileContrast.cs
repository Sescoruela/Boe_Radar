namespace BoeRadar.Application;

public sealed record SourceProfileDimension(string Key, string Label, string Status,
    string Message, IReadOnlyList<string> Quotes)
{
    public string Origin { get; init; } = "source";
    internal int PriorityContribution { get; init; }
}
public sealed record SourceProfileContrast(string SourceHash, IReadOnlyList<SourceProfileDimension> Dimensions);

/// <summary>Source-backed lexical hints, never an eligibility decision.</summary>
public static class SourceProfileContrastBuilder
{
    public static SourceProfileContrast Build(BusinessProfile profile, ActionableSourceReview review)
    {
        if (!BusinessProfileMatcher.IsValid(profile)) throw new ArgumentException("Perfil no válido.");
        return new(review.SourceHash,
        [
            Dimension("activity", "Actividad", BusinessProfileMatcher.Activities[profile.Activity],
                profile.Activity == "other", Quotes("recipients", "affected", "requirements", "obligations", "transitional")),
            RecipientDimension(),
            Dimension("territory", "Territorio", [.. BusinessProfileMatcher.Territories[profile.Territory], "ambito estatal", "ambito nacional", "territorio nacional"],
                profile.Territory == "all", Quotes("territory", "recipients", "affected", "requirements", "transitional"))
        ]);

        string[] Quotes(params string[] keys) => review.Groups.Where(group => keys.Contains(group.Key))
            .SelectMany(group => group.Quotes).Distinct(StringComparer.Ordinal).ToArray();

        SourceProfileDimension RecipientDimension()
        {
            var excludedIndividuals = profile.BusinessType == "autonomous"
                ? Quotes("recipients", "affected", "transitional").Where(quote => System.Text.RegularExpressions.Regex.IsMatch(
                    BusinessProfileMatcher.Normalize(quote),
                    @"(?<!no )\b(?:quedan|estan|seran|resultan) excluidas (?:las )?personas fisicas\b",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))).Take(2).ToArray()
                : [];
            if (excludedIndividuals.Length > 0) return new("businessType", "Tipo de negocio", "restriction",
                "El fragmento excluye expresamente a las personas físicas. Para tu perfil de autónomo es una posible restricción: revisa el contexto y los destinatarios completos, sin asumir elegibilidad ni exclusión automática.", excludedIndividuals);
            // Only explicit exclusive recipients, never a generic public-body reference or an application address.
            var exclusive = Quotes("recipients", "affected").Where(quote => System.Text.RegularExpressions.Regex.IsMatch(
                BusinessProfileMatcher.Normalize(quote),
                @"\b(?:beneficiari[oa]s?|destinatari[oa]s?)\s*(?::|seran|podran ser|son)?\s*(?:exclusivamente|unicamente|solo)\s+(?:las\s+|los\s+)?(?:entidades locales|ayuntamientos|administraciones publicas|universidades publicas|organismos publicos)\b",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))).Take(2).ToArray();
            if (exclusive.Length > 0) return new("businessType", "Tipo de negocio", "restriction",
                "El fragmento limita expresamente los destinatarios a entidades públicas, no al tipo de negocio indicado. Es un posible desajuste: comprueba la convocatoria completa antes de descartarla.", exclusive);
            return Dimension("businessType", "Tipo de negocio", BusinessProfileMatcher.TypeTerms(profile), false,
                Quotes("recipients", "affected", "requirements", "obligations", "transitional"));
        }
    }

    internal static bool HasRestriction(string normalizedText) => System.Text.RegularExpressions.Regex.IsMatch(normalizedText,
        @"\b(no podran|no pueden|excluid[oa]s?|exclusiones?|excepto|salvo|no seran|no tendran|no resulta aplicable|no es aplicable)\b",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    internal static SourceProfileDimension Dimension(string key, string label, string[] terms, bool unspecified,
        IEnumerable<string> quotes, string origin = "source")
    {
        if (unspecified) return new(key, label, "notSpecified",
            "No has concretado este dato del perfil; complétalo para afinar las pistas y comprueba el alcance en la fuente.", []) { Origin = origin };
        var matching = quotes.Select(quote => (Quote: quote, Text: BusinessProfileMatcher.Normalize(quote)))
            .Where(passage => terms.Any(term => BusinessProfileMatcher.Contains(passage.Text, term))).ToArray();
        var restrictions = matching.Where(passage => HasRestriction(passage.Text)).Select(passage => passage.Quote).Take(2).ToArray();
        if (restrictions.Length > 0) return new(key, label, "restriction",
            "La mención aparece junto a una posible restricción o excepción. No confirma que estés excluido ni que cumplas las condiciones: revisa el contexto completo.", restrictions) { Origin = origin };
        if (matching.Length > 0) return new(key, label, "mention",
            "Hay términos relacionados con tu perfil; una mención no confirma que te aplique. Revisa las condiciones y exclusiones.", matching.Take(2).Select(passage => passage.Quote).ToArray()) { Origin = origin };
        return new(key, label, "unknown", origin == "source"
            ? "Sin mención identificada en los fragmentos revisados. No significa que esté excluido: consulta el documento completo."
            : "El título y el epígrafe no permiten relacionar este dato con tu perfil. Comprueba el texto oficial.", []) { Origin = origin };
    }
}
