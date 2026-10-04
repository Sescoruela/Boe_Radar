using BoeRadar.Application;

namespace BoeRadar.UnitTests;

public sealed class BusinessPersonalizationTests
{
    private static readonly BusinessProfile Retail = new("autonomous", "retail", "baleares");
    private static PublicationListItem Item(int number, string title, string? epigraph = null,
        string department = "Ministerio de Hacienda") => new(Guid.NewGuid(), $"BOE-A-2026-{number}",
        new DateOnly(2026, 9, 26), title, "1", "Disposiciones generales", department, epigraph, null, null);

    [Fact]
    public void AccentedActivityAndTerritoryMentionsPrioritizeButDoNotConfirmEligibility()
    {
        var match = BusinessProfileMatcher.Match(Retail, Item(1,
            "Precios de tabaco en Expendedurías de Península y Baleares"));
        Assert.Equal(70, match.Priority);
        Assert.Equal(2, match.Reasons.Count);
        Assert.Contains(match.Checks, check => check.Contains("no garantiza cobertura"));
        Assert.Contains(match.Checks, check => check.Contains("destinatarios"));
    }

    [Fact]
    public void IssuerLocationCannotBeUsedAsTerritorialScope()
    {
        var match = BusinessProfileMatcher.Match(Retail, Item(2, "Cambio normativo general",
            department: "Gobierno de Baleares"));
        Assert.Equal(0, match.Priority);
        Assert.Contains(match.Checks, check => check.Contains("territorial pendiente"));
    }

    [Fact]
    public void UnknownScopeRemainsVisibleAndRequestsReview()
    {
        var match = BusinessProfileMatcher.Match(Retail, Item(3, "Convenio de Seguridad Social con Filipinas"));
        Assert.Equal("Alcance por comprobar", match.Label);
        Assert.Equal(3, match.Checks.Count);
    }

    [Theory]
    [InlineData("Organismos autónomos", 0)]
    [InlineData("Ayudas a trabajadores autónomos", 20)]
    [InlineData("Microempresas y pymes", 0)]
    public void BusinessTypeRequiresExplicitSelfEmploymentContext(string title, int expected)
    {
        Assert.Equal(expected, BusinessProfileMatcher.Match(
            new("autonomous", "other", "all"), Item(4, title)).Priority);
    }

    [Fact]
    public void SmeMentionAndNationalScopeAreHints()
    {
        var match = BusinessProfileMatcher.Match(new("sme", "other", "canarias"),
            Item(5, "Subvenciones a pymes de ámbito estatal"));
        Assert.Equal(25, match.Priority);
        Assert.Contains(match.Checks, check => check.Contains("exclusiones territoriales"));
    }

    [Theory]
    [InlineData("company", "retail", "all")]
    [InlineData("sme", "invalid", "all")]
    [InlineData("sme", "retail", "unknown")]
    public void InvalidProfilesAreRejected(string type, string activity, string territory)
    {
        Assert.False(BusinessProfileMatcher.IsValid(new(type, activity, territory)));
    }

    [Fact]
    public async Task RanksAcrossCatalogPagesBeforePagingWithoutHidingUnknownSignals()
    {
        var items = Enumerable.Range(1, 101).Select(index => Item(index,
            index == 101 ? "Normas para comercio" : "Señal general pendiente de revisar")).ToArray();
        var service = new PersonalizedPublicationSearch(new FakeCatalog(items));
        var result = await service.ExecuteAsync(Retail, new(null, null, null, null, 1, 12));
        Assert.Equal("BOE-A-2026-101", result.Items[0].ExternalId);
        Assert.Equal(101, result.TotalItems);
        Assert.Equal(9, result.TotalPages);
        Assert.False(result.IsPartial);
        Assert.Equal(12, result.Items.Count);
        var last = await service.ExecuteAsync(Retail, new(null, null, null, null, 9, 12));
        Assert.Equal(5, last.Items.Count);
        Assert.All(last.Items, item => Assert.Equal(0, item.ProfileMatch!.Priority));
    }

    [Fact]
    public async Task BoundedScanReportsPartialCoverage()
    {
        var items = Enumerable.Range(1, 1001).Select(index => Item(index, "Señal general")).ToArray();
        var result = await new PersonalizedPublicationSearch(new FakeCatalog(items))
            .ExecuteAsync(Retail, new(null, null, null, null));
        Assert.True(result.IsPartial);
        Assert.Equal(1001, result.CatalogSignalCount);
        Assert.Equal(1000, result.TotalItems);
    }

    [Fact]
    public async Task ReadsCandidateWindowOnceAndPreservesResultsAcrossPages()
    {
        var items = Enumerable.Range(1, 1001).Select(index => Item(index,
            index % 11 == 0 ? "Ayudas al comercio de Baleares" : "Señal general")).ToArray();
        var catalog = new FakeCatalog(items);
        var service = new PersonalizedPublicationSearch(catalog);
        var expected = items.Take(1000).Select(item => item with { ProfileMatch = BusinessProfileMatcher.Match(Retail, item) })
            .OrderByDescending(item => item.ProfileMatch!.Priority).ThenByDescending(item => item.PublicationDate)
            .ThenBy(item => item.ExternalId, StringComparer.Ordinal).ToArray();
        var result = await service.ExecuteAsync(Retail, new(null, null, null, null, 5, 20));
        Assert.Equal(1, catalog.CandidateReads);
        Assert.Equal(expected.Skip(80).Take(20).Select(item => item.Id), result.Items.Select(item => item.Id));
        Assert.True(result.IsPartial);
        Assert.Equal(1001, result.CatalogSignalCount);
    }

    [Fact]
    public async Task StoredEvidenceRanksBeforePagingAndSnapshotExcludesLaterReviews()
    {
        var items = Enumerable.Range(1, 4).Select(index => Item(index, "Señal general")).ToArray();
        var store = new FakeReviewStore();
        var service = new PersonalizedPublicationSearch(new FakeCatalog(items), store);
        var profile = new BusinessProfile("sme", "technology", "all");
        var first = await service.ExecuteAsync(profile, new(null, null, null, null, 1, 2));
        var later = first.EvidenceAsOf.AddSeconds(1);
        store.Reviews[items[3].ExternalId] = (later,
            new("hash", later, [new("recipients", "Destinatarios", ["Innovación para pymes."])]));
        var second = await service.ExecuteAsync(profile, new(null, null, null, null, 2, 2)
            { EvidenceAsOf = first.EvidenceAsOf.ToOffset(TimeSpan.FromHours(2)) });
        Assert.Equal(first.EvidenceAsOf, second.EvidenceAsOf);
        Assert.Equal(TimeSpan.Zero, second.EvidenceAsOf.Offset);
        Assert.Equal(0, second.EvidenceReviewedCount);
        Assert.Equal(4, first.Items.Concat(second.Items).Select(item => item.Id).Distinct().Count());
        var refreshed = await service.ExecuteAsync(profile, new(null, null, null, null, 1, 2)
            { EvidenceAsOf = later });
        Assert.Equal(items[3].ExternalId, refreshed.Items[0].ExternalId);
        Assert.Equal(1, refreshed.EvidenceReviewedCount);
        Assert.Equal(4, refreshed.TotalItems);
    }

    private sealed class FakeReviewStore : ISourceReviewStore
    {
        public Dictionary<string, (DateTimeOffset RecordedAt, ActionableSourceReview Review)> Reviews { get; } = [];
        public Task SaveAsync(string externalId, ActionableSourceReview review, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyDictionary<string, ActionableSourceReview>> GetAsync(IReadOnlyList<string> externalIds,
            DateTimeOffset asOf, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, ActionableSourceReview>>(Reviews
                .Where(pair => externalIds.Contains(pair.Key) && pair.Value.RecordedAt <= asOf)
                .ToDictionary(pair => pair.Key, pair => pair.Value.Review));
    }

    private sealed class FakeCatalog(PublicationListItem[] items) : IPublicationCatalog
    {
        public int CandidateReads { get; private set; }
        public Task<PagedResult<PublicationListItem>> GetBusinessCandidatesAsync(PublicationSearch search,
            CancellationToken cancellationToken = default)
        {
            CandidateReads++;
            return SearchAsync(search with { Page = 1, PageSize = 1000, BusinessSignalsOnly = true }, cancellationToken);
        }
        public Task<PagedResult<PublicationListItem>> SearchAsync(PublicationSearch search,
            CancellationToken cancellationToken = default)
        {
            Assert.True(search.BusinessSignalsOnly);
            return Task.FromResult(new PagedResult<PublicationListItem>(
                items.Skip((search.Page - 1) * search.PageSize).Take(search.PageSize).ToArray(),
                search.Page, search.PageSize, items.Length,
                (int)Math.Ceiling(items.Length / (double)search.PageSize)));
        }
        public Task<CatalogStatus> GetStatusAsync(bool enabled, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PublicationDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
