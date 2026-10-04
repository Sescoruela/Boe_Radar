namespace BoeRadar.Application;

public interface ISourceReviewStore
{
    Task SaveAsync(string externalId, ActionableSourceReview review, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, ActionableSourceReview>> GetAsync(IReadOnlyList<string> externalIds,
        DateTimeOffset asOf, CancellationToken cancellationToken = default);
}

public static class SourceEvidenceRanking
{
    public static BusinessProfileMatch Match(BusinessProfile profile, PublicationListItem item,
        ActionableSourceReview? review)
    {
        var initial = BusinessProfileMatcher.Match(profile, item);
        if (review is null) return initial;
        var contrast = SourceProfileContrastBuilder.Build(profile, review);
        var mentions = contrast.Dimensions.Where(dimension => dimension.Status == "mention" &&
            !dimension.Quotes.Any(quote => System.Text.RegularExpressions.Regex.IsMatch(
                BusinessProfileMatcher.Normalize(quote),
                @"\b(no podran|no pueden|excluid[oa]s?|exclusiones?|excepto|salvo|no seran|no tendran)\b",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))))
            .ToArray();
        var bonus = mentions.Sum(dimension => dimension.Key switch
        { "activity" => 80, "businessType" => 40, "territory" => 20, _ => 0 });
        return initial with
        {
            Priority = initial.Priority + bonus,
            Label = bonus > 0 ? "Menciones en título o texto oficial" : initial.Label,
            Reasons = (initial.Priority == 0 && bonus > 0 ? Array.Empty<string>() : initial.Reasons).Concat(mentions.Select(dimension =>
                $"Los fragmentos oficiales mencionan términos de tu {dimension.Label.ToLowerInvariant()}; revisa las condiciones en la ficha.")).ToArray(),
            Checks = initial.Checks.Append("Las menciones del texto no confirman elegibilidad ni obligaciones; comprueba exclusiones y vigencia.").ToArray(),
            SourceHash = review.SourceHash
        };
    }
}
