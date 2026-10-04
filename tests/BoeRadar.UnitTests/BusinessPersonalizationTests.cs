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

    private sealed class FakeCatalog(PublicationListItem[] items) : IPublicationCatalog
    {
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
