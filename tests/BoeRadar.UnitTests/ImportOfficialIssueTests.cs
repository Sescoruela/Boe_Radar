using BoeRadar.Application;

namespace BoeRadar.UnitTests;

public sealed class ImportOfficialIssueTests
{
    [Fact]
    public async Task ExecuteAsync_PassesFetchedIssueToStore()
    {
        var date = new DateOnly(2026, 9, 22);
        var issue = new OfficialIssue(
            "BOE",
            date,
            new Uri("https://www.boe.es/summary"),
            []);
        var source = new FakeSource(issue);
        var store = new FakeStore();
        var sut = new ImportOfficialIssue(source, store);

        var result = await sut.ExecuteAsync(date, "scheduled");

        Assert.Same(issue, store.ReceivedIssue);
        Assert.Equal("scheduled", store.ReceivedTrigger);
        Assert.Equal(date, result.PublicationDate);
    }

    private sealed class FakeSource(OfficialIssue issue) : IOfficialGazetteSource
    {
        public string SourceCode => issue.SourceCode;

        public Task<OfficialIssue> GetIssueAsync(
            DateOnly publicationDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(issue);
    }

    private sealed class FakeStore : IIngestionStore
    {
        public OfficialIssue? ReceivedIssue { get; private set; }

        public string? ReceivedTrigger { get; private set; }

        public Task<ImportIssueResult> ImportAsync(
            OfficialIssue issue,
            string trigger,
            CancellationToken cancellationToken = default)
        {
            ReceivedIssue = issue;
            ReceivedTrigger = trigger;
            return Task.FromResult(
                new ImportIssueResult(
                    Guid.CreateVersion7(),
                    issue.PublicationDate,
                    issue.Documents.Count,
                    0,
                    0,
                    issue.Documents.Count));
        }
    }
}

