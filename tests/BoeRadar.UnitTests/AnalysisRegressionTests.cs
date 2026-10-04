using BoeRadar.Application;
using BoeRadar.Domain;
using BoeRadar.Infrastructure.Analysis;

namespace BoeRadar.UnitTests;

public sealed class AnalysisRegressionTests
{
    [Fact]
    public void GeminiRejectsOversizeSourceRatherThanSilentlyTruncatingIt()
    {
        var complete = new DocumentText(new string('a', 40_000), "hash", "xml");
        Assert.Equal(complete.Text, GeminiDocumentAnalyzer.GetCompleteSource(complete));
        Assert.Throws<AnalysisSourceTooLongException>(() => GeminiDocumentAnalyzer.GetCompleteSource(
            complete with { Text = complete.Text + "b" }));
    }
    [Theory]
    [InlineData("2026-10-15", "15 de octubre", true)]
    [InlineData("2026-10-16", "15 de octubre de 2026", true)]
    [InlineData("15/10/2026", "15 de octubre de 2026", true)]
    [InlineData("2026-10-15", "15 de octubre de 2026", false)]
    public void RejectsUnsupportedOrCalculatedDate(string date, string quote, bool explicitDate)
    {
        var output = Output() with
        {
            Deadlines = [new(date, "Solicitudes", explicitDate)],
            Evidence = [new(quote, "deadlines[0]")]
        };
        Assert.Throws<InvalidOperationException>(() => new AnalysisValidator().Validate(output, quote));
    }

    [Theory]
    [InlineData("15 de octubre de 2026")]
    [InlineData("2026-10-15")]
    [InlineData("15/10/2026")]
    public void AcceptsCompleteDateWithSpecificCitation(string quote)
    {
        var output = Output() with
        {
            Deadlines = [new("2026-10-15", "Solicitudes", true)],
            Evidence = [new(quote, "deadlines[0]")]
        };
        new AnalysisValidator().Validate(output, quote);
    }

    [Fact]
    public void RejectsRequirementWithOnlyGenericEvidence()
    {
        var output = Output() with { Requirements = ["Ser pyme"] };
        Assert.Throws<InvalidOperationException>(() => new AnalysisValidator().Validate(output, "Ayudas"));
    }

    [Fact]
    public void RejectsInventedRequirementEvenWithSpecificEvidenceTag()
    {
        var output = Output() with
        {
            Requirements = ["Facturar más de un millón"],
            Evidence = [new("Ayudas", "requirements[0]")]
        };
        Assert.Throws<InvalidOperationException>(() => new AnalysisValidator().Validate(output, "Ayudas"));
    }

    [Fact]
    public async Task ScansPastFirstHundredAndSkipsExistingAnalyses()
    {
        var store = new FakeStore(205);
        store.Saved.UnionWith(store.Documents.Take(200).Select(item => item.DocumentId));
        var result = await Pipeline(store).ExecuteAsync(new(2026, 10, 1), 2);
        Assert.Equal(202, result.Scanned);
        Assert.Equal(2, result.Analyzed);
        Assert.NotNull(result.NextCursor);
        var rest = await Pipeline(store).ExecuteAsync(new(2026, 10, 1), 100, afterExternalId: result.NextCursor);
        Assert.Equal(3, rest.Analyzed);
        Assert.Null(rest.NextCursor);
    }

    [Fact]
    public async Task SingleFailureDoesNotAbortBatchAndIsExplicitlyReported()
    {
        var store = new FakeStore(3);
        store.FailId = store.Documents[0].DocumentId;
        var result = await Pipeline(store).ExecuteAsync(new(2026, 10, 1));
        Assert.Equal(2, result.Analyzed);
        Assert.Equal(store.Documents[0].ExternalId, Assert.Single(result.Failures!).ExternalId);
        store.FailId = null;
        var retry = await Pipeline(store).ExecuteAsync(new(2026, 10, 1));
        Assert.Equal(1, retry.Analyzed);
        Assert.Empty(retry.Failures!);
    }

    [Fact]
    public async Task CancellationIsNotConvertedIntoDocumentFailure()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Pipeline(new FakeStore(1)).ExecuteAsync(new(2026, 10, 1), cancellationToken: canceled.Token));
    }

    private static RadarAnalysisOutput Output() => new(true, RadarCategory.Grant, "Resumen", [], [],
        [new("Ayudas", "relevancia")], .9m);

    private static AnalyzeRadarDocuments Pipeline(FakeStore store) => new(store, new FakeSource(),
        new FakeAnalyzer(), new CandidatePrefilter(), new AnalysisValidator());

    private sealed class FakeSource : IOfficialDocumentTextSource
    {
        public Task<DocumentText> GetAsync(Uri url, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DocumentText("Ayudas", new string('a', 64), "xml"));
    }

    private sealed class FakeAnalyzer : IDocumentAnalyzer
    {
        public string Method => "test";
        public string ModelName => "test";
        public string PromptVersion => "radar-v2";
        public Task<RadarAnalysisOutput> AnalyzeAsync(AnalysisCandidate candidate, DocumentText content,
            CancellationToken cancellationToken = default) => Task.FromResult(Output());
    }

    private sealed class FakeStore(int count) : IAnalysisCandidateStore
    {
        public List<AnalysisCandidate> Documents { get; } = Enumerable.Range(1, count).Select(index =>
            new AnalysisCandidate(Guid.NewGuid(), $"BOE-A-2026-{index:00000}", new(2026, 10, 1),
                "Ayudas para pymes", "Ministerio", "1", null, new Uri($"https://www.boe.es/{index}"))).ToList();
        public HashSet<Guid> Saved { get; } = [];
        public Guid? FailId { get; set; }
        public Task<IReadOnlyList<AnalysisCandidate>> GetByDateAsync(DateOnly date, int limit,
            CancellationToken cancellationToken = default, string? afterExternalId = null) =>
            Task.FromResult<IReadOnlyList<AnalysisCandidate>>(Documents.Where(item => afterExternalId is null ||
                string.CompareOrdinal(item.ExternalId, afterExternalId) > 0).Take(limit).ToArray());
        public Task<bool> ExistsAsync(Guid id, string hash, string model, string version,
            CancellationToken cancellationToken = default) => Task.FromResult(Saved.Contains(id));
        public Task SaveAsync(Guid id, string hash, RadarAnalysisOutput output, string method,
            string model, string version, CancellationToken cancellationToken = default)
        {
            if (id == FailId) throw new InvalidOperationException("Intentional fixture failure");
            Saved.Add(id);
            return Task.CompletedTask;
        }
    }
}
