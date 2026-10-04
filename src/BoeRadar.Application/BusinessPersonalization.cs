using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BoeRadar.Application;

public sealed record BusinessProfile(string BusinessType, string Activity, string Territory);
public sealed record BusinessProfileMatch(int Priority, string Label,
    IReadOnlyList<string> Reasons, IReadOnlyList<string> Checks)
{
    public string? SourceHash { get; init; }
}
public sealed record PersonalizedSearchRequest(BusinessProfile Profile, PublicationSearch Search);
public sealed record PersonalizedSearchResult(IReadOnlyList<PublicationListItem> Items,
    int Page, int PageSize, int TotalItems, int TotalPages, int CatalogSignalCount, bool IsPartial)
{
    public DateTimeOffset EvidenceAsOf { get; init; }
    public int EvidenceReviewedCount { get; init; }
}

// These are metadata hints for ordering, never an eligibility determination.
public static class BusinessProfileMatcher
{
    public static readonly IReadOnlyDictionary<string, string[]> Activities =
        new Dictionary<string, string[]>
        {
            ["other"] = [],
            ["retail"] = ["comercio", "comercial", "tabaco", "expendedurias"],
            ["hospitality"] = ["hosteleria", "turismo", "turistico", "restauracion"],
            ["construction"] = ["construccion", "edificacion", "rehabilitacion", "vivienda"],
            ["technology"] = ["digitalizacion", "tecnologia", "tecnologicas", "innovacion", "investigacion", "i+d+i"],
            ["professional"] = ["profesionales", "arquitectura", "asesoria", "extranjeria", "extranjeros"],
            ["agriculture"] = ["agricultura", "agrario", "agrarias", "ganaderia", "ganadero", "pesca"],
            ["transport"] = ["transporte", "logistica", "movilidad", "auto+"],
            ["educationSport"] = ["formacion", "educacion", "deporte", "deportivo"],
        };

    public static readonly IReadOnlyDictionary<string, string[]> Territories =
        new Dictionary<string, string[]>
        {
            ["all"] = [], ["andalucia"] = ["andalucia"], ["aragon"] = ["aragon"],
            ["asturias"] = ["asturias"], ["baleares"] = ["baleares", "illes balears"],
            ["canarias"] = ["canarias"], ["cantabria"] = ["cantabria"],
            ["castillaLaMancha"] = ["castilla-la mancha", "castilla la mancha"],
            ["castillaLeon"] = ["castilla y leon"], ["cataluna"] = ["cataluna", "catalunya"],
            ["valencia"] = ["comunitat valenciana", "comunidad valenciana"],
            ["extremadura"] = ["extremadura"], ["galicia"] = ["galicia"],
            ["madrid"] = ["comunidad de madrid"], ["murcia"] = ["region de murcia"],
            ["navarra"] = ["navarra"], ["paisVasco"] = ["pais vasco", "euskadi"],
            ["laRioja"] = ["la rioja"], ["ceuta"] = ["ceuta"], ["melilla"] = ["melilla"],
        };

    public static bool IsValid(BusinessProfile? profile) => profile is not null &&
        profile.BusinessType is "autonomous" or "sme" &&
        profile.Activity is not null && Activities.ContainsKey(profile.Activity) &&
        profile.Territory is not null && Territories.ContainsKey(profile.Territory);

    public static BusinessProfileMatch Match(BusinessProfile profile, PublicationListItem item)
    {
        if (!IsValid(profile)) throw new ArgumentException("Perfil de negocio no válido.", nameof(profile));
        // Do not use the issuer's address or ministry name to infer territorial scope.
        var text = Normalize($"{item.Title} {item.Epigraph}");
        var reasons = new List<string>();
        var checks = new List<string>();
        var priority = 0;
        if (Activities[profile.Activity].Any(term => Contains(text, term)))
        {
            priority += 60;
            reasons.Add("El título o epígrafe menciona términos relacionados con tu actividad.");
        }
        else checks.Add("Comprueba si el contenido afecta a tu actividad; el título no permite determinarlo.");

        var typeTerms = profile.BusinessType == "autonomous"
            ? new[] { "trabajador autonomo", "trabajadores autonomos", "profesionales autonomos", "personas autonomas" }
            : new[] { "pymes", "pyme", "pequenas y medianas empresas" };
        if (typeTerms.Any(term => Contains(text, term)))
        {
            priority += 20;
            reasons.Add("El título o epígrafe menciona tu tipo de negocio.");
        }
        else checks.Add("Verifica los destinatarios y las condiciones para autónomos o pymes.");

        if (profile.Territory != "all" && Territories[profile.Territory].Any(term => Contains(text, term)))
        {
            priority += 10;
            reasons.Add("El título o epígrafe menciona el territorio que has indicado.");
            checks.Add("Confirma el ámbito territorial en la fuente; una mención no garantiza cobertura.");
        }
        else if (Contains(text, "ambito estatal") || Contains(text, "ambito nacional"))
        {
            priority += 5;
            reasons.Add("El título o epígrafe indica ámbito estatal o nacional.");
            checks.Add("Revisa las condiciones y posibles exclusiones territoriales.");
        }
        else checks.Add("Ámbito territorial pendiente de comprobar en el texto oficial.");

        if (reasons.Count == 0) reasons.Add("Es una señal para negocios que conviene revisar; faltan datos para vincularla a tu perfil.");
        return new(priority, priority > 0 ? "Coincidencias con tu perfil" : "Alcance por comprobar", reasons, checks);
    }

    internal static bool Contains(string text, string term) => Regex.IsMatch(text,
        $@"(?<![a-z0-9]){Regex.Escape(term)}(?![a-z0-9])", RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    internal static string Normalize(string value)
    {
        var result = new StringBuilder();
        foreach (var character in value.ToLowerInvariant().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                result.Append(character);
        return result.ToString().Normalize(NormalizationForm.FormC);
    }
}

public sealed class PersonalizedPublicationSearch(IPublicationCatalog catalog,
    ISourceReviewStore? sourceReviews = null, TimeProvider? clock = null)
{
    public async Task<PersonalizedSearchResult> ExecuteAsync(BusinessProfile profile,
        PublicationSearch search, CancellationToken cancellationToken = default)
    {
        if (!BusinessProfileMatcher.IsValid(profile)) throw new ArgumentException("Perfil no válido.");
        // Preserve the 1,000-signal bound without repeating counts and analysis queries per page.
        var candidates = await catalog.GetBusinessCandidatesAsync(search, cancellationToken);
        var catalogCount = candidates.TotalItems;
        var snapshot = (search.EvidenceAsOf ?? (clock ?? TimeProvider.System).GetUtcNow()).ToUniversalTime();
        var unique = candidates.Items.DistinctBy(item => item.Id).ToArray();
        var evidence = sourceReviews is null || unique.Length == 0 ? new Dictionary<string, ActionableSourceReview>()
            : await sourceReviews.GetAsync(unique.Select(item => item.ExternalId).ToArray(), snapshot, cancellationToken);
        var ordered = unique
            .Select(item => item with { ProfileMatch = SourceEvidenceRanking.Match(profile, item,
                evidence.GetValueOrDefault(item.ExternalId)) })
            .OrderByDescending(item => item.ProfileMatch!.Priority)
            .ThenByDescending(item => item.PublicationDate).ThenBy(item => item.ExternalId, StringComparer.Ordinal)
            .ToArray();
        var page = Math.Max(1, search.Page);
        var pageSize = Math.Clamp(search.PageSize, 1, 100);
        return new(ordered.Skip((int)Math.Min((long)(page - 1) * pageSize, ordered.Length)).Take(pageSize).ToArray(),
            page, pageSize, ordered.Length, (int)Math.Ceiling(ordered.Length / (double)pageSize),
            catalogCount, ordered.Length < catalogCount)
        { EvidenceAsOf = snapshot, EvidenceReviewedCount = unique.Count(item => evidence.ContainsKey(item.ExternalId)) };
    }
}
