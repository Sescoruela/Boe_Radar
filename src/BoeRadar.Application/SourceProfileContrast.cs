namespace BoeRadar.Application;

public sealed record SourceProfileDimension(string Key, string Label, string Status,
    string Message, IReadOnlyList<string> Quotes);
public sealed record SourceProfileContrast(string SourceHash, IReadOnlyList<SourceProfileDimension> Dimensions);

/// <summary>Source-backed lexical hints, never an eligibility decision.</summary>
public static class SourceProfileContrastBuilder
{
    public static SourceProfileContrast Build(BusinessProfile profile, ActionableSourceReview review)
    {
        if (!BusinessProfileMatcher.IsValid(profile)) throw new ArgumentException("Perfil no válido.");
        var quotes = review.Groups.SelectMany(group => group.Quotes).Distinct(StringComparer.Ordinal).ToArray();
        string[] typeTerms = profile.BusinessType == "autonomous"
            ? ["trabajador autonomo", "trabajadores autonomos", "profesionales autonomos", "personas autonomas",
                "trabajador por cuenta propia", "trabajadores por cuenta propia",
                "personas fisicas que desarrollen actividades economicas"]
            : ["pymes", "pyme", "pequenas y medianas empresas"];
        return new(review.SourceHash,
        [
            Dimension("activity", "Actividad", BusinessProfileMatcher.Activities[profile.Activity],
                profile.Activity == "other"),
            Dimension("businessType", "Tipo de negocio", typeTerms, false),
            Dimension("territory", "Territorio", BusinessProfileMatcher.Territories[profile.Territory],
                profile.Territory == "all")
        ]);

        SourceProfileDimension Dimension(string key, string label, string[] terms, bool unspecified)
        {
            if (unspecified) return new(key, label, "notSpecified",
                "No has concretado este dato del perfil; comprueba el alcance en la fuente.", []);
            var evidence = quotes.Where(quote => terms.Any(term =>
                BusinessProfileMatcher.Contains(BusinessProfileMatcher.Normalize(quote), term)))
                .Take(2).ToArray();
            if (evidence.Length > 0) return new(key, label, "mention",
                "Hay términos relacionados con tu perfil. Revisa las condiciones y exclusiones del fragmento; una mención no confirma que te aplique.", evidence);
            return new(key, label, "unknown",
                "Sin mención identificada en los fragmentos revisados. No significa que esté excluido: consulta el documento completo.", []);
        }
    }
}
