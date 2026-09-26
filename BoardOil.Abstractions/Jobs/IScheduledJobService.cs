using BoardOil.Contracts.Common;
using BoardOil.Contracts.Jobs;

namespace BoardOil.Abstractions.Jobs;

public interface IScheduledJobService
{
    Task<IReadOnlyList<ScheduledJobEnqueueResult>> EnqueueDueJobsAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<IReadOnlyList<ScheduledJobDto>>> ListAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<RunScheduledJobNowResultDto>> RunNowAsync(
        string scheduleName, int? userId = null, CancellationToken cancellationToken = default);
}

public interface IScheduledJobDefinition
{
    string Name { get; }
    string DisplayName { get; }
    string SchedulerStateName { get; }
    Task<ScheduledJobDefinitionConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScheduledJobOccurrence>> CreateOccurrencesAsync(
        DateTime dueAtUtc, CancellationToken cancellationToken = default);
}

public interface ICronOccurrenceCalculator
{
    DateTime? GetLatestOccurrence(
        string cronExpression, TimeZoneInfo timeZone, DateTime fromUtcExclusive, DateTime throughUtcInclusive);
    DateTime? GetNextOccurrence(string cronExpression, TimeZoneInfo timeZone, DateTime afterUtc);
    void Validate(string cronExpression);
}

public sealed record ScheduledJobDefinitionConfiguration(
    bool Enabled, string CronExpression, bool RunOnInitialisation = false);

// TargetKey is required for every occurrence when a definition fans out. Keep it stable across retries.
public sealed record ScheduledJobOccurrence(
    string JobType, string PayloadJson = "{}", string? TargetKey = null, int? UserId = null);

public sealed record ScheduledJobEnqueueResult(
    string ScheduleName, DateTime DueAtUtc, string CorrelationId, bool Enqueued, int? JobId);
