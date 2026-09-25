namespace BoeRadar.Domain;

public enum IngestionRunStatus
{
    Running,
    Completed,
    Partial,
    Failed
}

public sealed class IngestionRun
{
    private IngestionRun()
    {
    }

    private IngestionRun(
        Guid sourceId,
        DateOnly targetDate,
        string trigger,
        DateTimeOffset startedAt)
    {
        Id = Guid.CreateVersion7();
        SourceId = sourceId;
        TargetDate = targetDate;
        Trigger = trigger;
        Status = IngestionRunStatus.Running;
        StartedAt = startedAt;
        Counters = "{}";
    }

    public Guid Id { get; private set; }

    public Guid SourceId { get; private set; }

    public DateOnly TargetDate { get; private set; }

    public string Trigger { get; private set; } = string.Empty;

    public IngestionRunStatus Status { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public string Counters { get; private set; } = "{}";

    public string? ErrorSummary { get; private set; }

    public static IngestionRun Start(
        Guid sourceId,
        DateOnly targetDate,
        string trigger,
        DateTimeOffset startedAt) =>
        new(sourceId, targetDate, trigger, startedAt);

    public void Complete(string counters, DateTimeOffset finishedAt)
    {
        Status = IngestionRunStatus.Completed;
        Counters = counters;
        FinishedAt = finishedAt;
        ErrorSummary = null;
    }

    public void Fail(string errorSummary, DateTimeOffset finishedAt)
    {
        Status = IngestionRunStatus.Failed;
        FinishedAt = finishedAt;
        ErrorSummary = errorSummary[..Math.Min(errorSummary.Length, 2_000)];
    }
}

