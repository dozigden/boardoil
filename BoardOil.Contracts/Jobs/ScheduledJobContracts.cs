namespace BoardOil.Contracts.Jobs;

public sealed record ScheduledJobDto(
    string Name, string DisplayName, bool Enabled, string CronExpression,
    string TimeZoneId, DateTime? LastEvaluatedAtUtc, DateTime? NextOccurrenceUtc,
    ScheduledJobRunDto? CurrentJob, ScheduledJobRunDto? LatestStartedJob);

public sealed record ScheduledJobRunDto(int Id, string Status, DateTime? StartedAtUtc);

public sealed record RunScheduledJobNowResultDto(
    int EnqueuedCount, IReadOnlyList<int> JobIds);
