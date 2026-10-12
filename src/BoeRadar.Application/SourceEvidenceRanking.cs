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
            initial.Dimensions.Single(metadata => metadata.Key == dimension.Key).Status != "restriction").ToArray();
        var restrictions = contrast.Dimensions.Where(dimension => dimension.Status == "restriction").ToArray();
        // A restrictive source passage must not retain a positive metadata score for the same dimension.
        var reduction = restrictions.Sum(dimension => initial.Dimensions.Single(metadata => metadata.Key == dimension.Key).PriorityContribution);
        var dimensions = initial.Dimensions.Select(metadata => {
            var source = contrast.Dimensions.Single(dimension => dimension.Key == metadata.Key);
            if (source.Status == "restriction") return source;
            if (metadata.Status == "restriction") return metadata;
            return source.Status == "mention" ? source : metadata;
        }).ToArray();
        var bonus = mentions.Sum(dimension => dimension.Key switch
        { "activity" => 80, "businessType" => 40, "territory" => 20, _ => 0 });
        return initial with
        {
            Priority = Math.Max(0, initial.Priority - reduction) + bonus,
            Label = restrictions.Length > 0 || dimensions.Any(dimension => dimension.Status == "restriction")
                ? "Posibles restricciones por revisar" : bonus > 0 ? "Menciones en título o texto oficial" : initial.Label,
            Reasons = (initial.Priority == 0 && bonus > 0 ? Array.Empty<string>() : initial.Reasons).Concat(mentions.Select(dimension =>
                $"Los fragmentos oficiales mencionan términos de tu {dimension.Label.ToLowerInvariant()}; revisa las condiciones en la ficha.")).ToArray(),
            Checks = initial.Checks.Concat(restrictions.Select(dimension => $"Posible restricción en {dimension.Label.ToLowerInvariant()} del texto oficial: comprueba la cita y su contexto."))
                .Append("Las menciones del texto no confirman elegibilidad ni obligaciones; comprueba exclusiones y vigencia.").ToArray(),
            SourceHash = review.SourceHash,
            Dimensions = dimensions
        };
    }
}
