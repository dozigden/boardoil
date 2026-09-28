using System.Globalization;
using BoardOil.Abstractions.Configuration;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Jobs;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.Jobs;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Jobs;

namespace BoardOil.Services.Jobs;

public sealed class ScheduledJobService : IScheduledJobService
{
    private readonly IDbContextScopeFactory _scopes;
    private readonly IJobRepository _jobs;
    private readonly IScheduledJobSchedulerStateRepository _states;
    private readonly IJobService _jobService;
    private readonly ISystemTimeZoneService _timeZones;
    private readonly IDailyOccurrenceCalculator _dailyOccurrences;
    private readonly SchedulingGate _gate;
    private readonly TimeProvider _clock;
    private readonly IReadOnlyList<IScheduledJobDefinition> _schedules;

    public ScheduledJobService(
        IDbContextScopeFactory scopes,
        IJobRepository jobs,
        IScheduledJobSchedulerStateRepository states,
        IJobService jobService,
        IEnumerable<IScheduledJobDefinition> schedules,
        ISystemTimeZoneService timeZones,
        IDailyOccurrenceCalculator dailyOccurrences,
        SchedulingGate gate,
        TimeProvider clock)
    {
        _scopes = scopes;
        _jobs = jobs;
        _states = states;
        _jobService = jobService;
        _timeZones = timeZones;
        _dailyOccurrences = dailyOccurrences;
        _gate = gate;
        _clock = clock;
        _schedules = schedules.ToArray();
        ValidateRegistrations(_schedules);
    }

    public async Task<IReadOnlyList<ScheduledJobEnqueueResult>> EnqueueDueJobsAsync(
        CancellationToken cancellationToken = default)
    {
        using var coordination = await _gate.EnterAsync(cancellationToken);
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var timeZone = await _timeZones.GetConfiguredTimeZoneAsync(cancellationToken);
        var results = new List<ScheduledJobEnqueueResult>();
        var failures = new List<Exception>();

        foreach (var schedule in _schedules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await EvaluateAsync(schedule, nowUtc, timeZone, results, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failures.Add(new InvalidOperationException(
                    $"Scheduled job evaluation failed for '{schedule.Name}'.", exception));
            }
        }

        try
        {
            await RemoveObsoleteCheckpointsAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            failures.Add(new InvalidOperationException("Obsolete scheduler checkpoint removal failed.", exception));
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                $"Scheduled job evaluation failed: {string.Join("; ", failures.Select(x => x.Message))}",
                failures);
        }

        return results;
    }

    public async Task<ApiResult<IReadOnlyList<ScheduledJobDto>>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var timeZone = await _timeZones.GetConfiguredTimeZoneAsync(cancellationToken);
        var items = new List<ScheduledJobDto>();
        foreach (var schedule in _schedules.OrderBy(x => x.DisplayName, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var configuration = await GetValidConfigurationAsync(schedule, cancellationToken);
            using var scope = _scopes.CreateReadOnly(DbContextScopeOption.ForceCreateNew);
            var state = await _states.GetByNameAsync(schedule.SchedulerStateName, cancellationToken);
            var scheduledPrefix = $"scheduled:{schedule.Name}:";
            var adHocPrefix = $"adhoc:{schedule.Name}:";
            var current = await _jobs.GetLatestActiveByCorrelationPrefixesAsync(
                scheduledPrefix, adHocPrefix, cancellationToken);
            var latestStarted = await _jobs.GetLatestStartedByCorrelationPrefixesAsync(
                scheduledPrefix, adHocPrefix, cancellationToken);
            DateTime? next = null;
            if (configuration.Enabled)
            {
                if (configuration.Kind == ScheduledJobKind.Once)
                {
                    if (state?.RunRequested == true) { next = nowUtc; }
                }
                else
                {
                    next = _dailyOccurrences.GetNextOccurrence(configuration.DailyTime!.Value, timeZone, nowUtc);
                }
            }
            items.Add(new ScheduledJobDto(
                schedule.Name, schedule.DisplayName, configuration.Enabled,
                configuration.DailyTime, timeZone.Id, state?.LastEvaluatedAtUtc,
                next, ToRunDto(current), ToRunDto(latestStarted),
                configuration.Kind.ToString().ToLowerInvariant()));
        }

        return ApiResults.Ok<IReadOnlyList<ScheduledJobDto>>(items);
    }

    public async Task<ApiResult<RunScheduledJobNowResultDto>> RunNowAsync(
        string scheduleName, int? userId = null, CancellationToken cancellationToken = default)
    {
        var schedule = _schedules.SingleOrDefault(x =>
            string.Equals(x.Name, scheduleName, StringComparison.OrdinalIgnoreCase));
        if (schedule is null)
        {
            return ApiErrors.NotFound("Scheduled job not found.");
        }

        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var occurrences = await schedule.CreateOccurrencesAsync(nowUtc, cancellationToken);
        ValidateOccurrences(schedule, occurrences, nowUtc);
        var runToken = string.Create(CultureInfo.InvariantCulture,
            $"{nowUtc:yyyyMMdd'T'HHmmss'Z'}:{Guid.NewGuid():N}");
        var correlationId = $"adhoc:{schedule.Name}:{runToken}";
        var jobIds = new List<int>();
        foreach (var occurrence in occurrences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await _jobService.EnqueueAsync(
                    new CreateJobRequest(
                        occurrence.JobType, occurrence.PayloadJson, nowUtc,
                        userId ?? occurrence.UserId, correlationId),
                    cancellationToken);
                if (!result.Success)
                {
                    return PartialRunFailure(schedule, jobIds, result.Message);
                }

                jobIds.Add(result.Data!.Id);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                return PartialRunFailure(schedule, jobIds, exception.Message);
            }
        }

        return ApiResults.Ok(new RunScheduledJobNowResultDto(jobIds.Count, jobIds));
    }

    private async Task RemoveObsoleteCheckpointsAsync(CancellationToken cancellationToken)
    {
        var registeredNames = _schedules.Select(schedule => schedule.SchedulerStateName)
            .ToHashSet(StringComparer.Ordinal);
        using var scope = _scopes.Create(DbContextScopeOption.ForceCreateNew);
        var states = await _states.ListAsync(cancellationToken);
        var obsoleteStates = states.Where(state => !registeredNames.Contains(state.Name)).ToArray();
        if (obsoleteStates.Length == 0) { return; }

        _states.RemoveRange(obsoleteStates);
        await scope.SaveChangesAsync(cancellationToken);
    }

    private async Task EvaluateAsync(
        IScheduledJobDefinition schedule,
        DateTime nowUtc,
        TimeZoneInfo timeZone,
        List<ScheduledJobEnqueueResult> results,
        CancellationToken cancellationToken)
    {
        var configuration = await GetValidConfigurationAsync(schedule, cancellationToken);
        EntityScheduledJobSchedulerState? state;
        using (var scope = _scopes.CreateReadOnly(DbContextScopeOption.ForceCreateNew))
        {
            state = await _states.GetByNameAsync(schedule.SchedulerStateName, cancellationToken);
        }

        if (configuration.Kind == ScheduledJobKind.Once)
        {
            if (!configuration.Enabled || state?.RunRequested != true) { return; }
        }
        else if (!configuration.Enabled || (state is null && !configuration.RunOnInitialisation))
        {
            await AdvanceCheckpointAsync(schedule, nowUtc, cancellationToken);
            return;
        }

        DateTime? dueAtUtc = state?.PendingDueAtUtc;
        if (dueAtUtc is null)
        {
            dueAtUtc = state is null || configuration.Kind == ScheduledJobKind.Once
                ? nowUtc
                : _dailyOccurrences.GetLatestOccurrence(
                    configuration.DailyTime!.Value, timeZone, state.LastRunTimeUtc, nowUtc);
        }

        if (dueAtUtc is not null)
        {
            if (state?.PendingDueAtUtc is null)
            {
                // Save the intended due time before jobs. A retry after a partial enqueue or
                // checkpoint failure resumes this identity even across a later daily boundary.
                await RecordPendingAsync(schedule, dueAtUtc.Value, cancellationToken);
            }

            var occurrences = await schedule.CreateOccurrencesAsync(dueAtUtc.Value, cancellationToken);
            ValidateOccurrences(schedule, occurrences, dueAtUtc.Value);
            var enqueueFailures = new List<string>();
            foreach (var occurrence in occurrences)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var correlationId = ScheduledJobCorrelationIds.Create(
                    schedule.Name, dueAtUtc.Value, occurrence.TargetKey);
                try
                {
                    using (var scope = _scopes.CreateReadOnly(DbContextScopeOption.ForceCreateNew))
                    {
                        if (await _jobs.ExistsByCorrelationIdAsync(correlationId, cancellationToken))
                        {
                            results.Add(new ScheduledJobEnqueueResult(
                                schedule.Name, dueAtUtc.Value, correlationId, false, null));
                            continue;
                        }
                    }

                    var result = await _jobService.EnqueueAsync(
                        new CreateJobRequest(
                            occurrence.JobType, occurrence.PayloadJson, dueAtUtc,
                            occurrence.UserId, correlationId),
                        cancellationToken);
                    if (!result.Success)
                    {
                        enqueueFailures.Add($"{correlationId}: {result.Message}");
                        continue;
                    }

                    results.Add(new ScheduledJobEnqueueResult(
                        schedule.Name, dueAtUtc.Value, correlationId, true, result.Data!.Id));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    enqueueFailures.Add($"{correlationId}: {exception.Message}");
                }
            }

            if (enqueueFailures.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Enqueue failed: {string.Join("; ", enqueueFailures)}");
            }
        }

        await AdvanceCheckpointAsync(schedule, nowUtc, cancellationToken,
            completeRequest: configuration.Kind == ScheduledJobKind.Once);
    }

    private async Task<ScheduledJobDefinitionConfiguration> GetValidConfigurationAsync(
        IScheduledJobDefinition schedule, CancellationToken cancellationToken)
    {
        var configuration = await schedule.GetConfigurationAsync(cancellationToken);
        if (configuration is null)
        {
            throw new InvalidOperationException($"Schedule '{schedule.Name}' returned no configuration.");
        }

        var valid = configuration.Kind switch
        {
            ScheduledJobKind.Daily => configuration.DailyTime is not null,
            ScheduledJobKind.Once => configuration.DailyTime is null && !configuration.RunOnInitialisation,
            _ => false
        };
        if (!valid)
        {
            throw new InvalidOperationException(
                $"Schedule '{schedule.Name}' must specify a daily time for Daily, or no time/initialisation run for Once.");
        }

        return configuration;
    }

    private async Task AdvanceCheckpointAsync(
        IScheduledJobDefinition schedule, DateTime nowUtc, CancellationToken cancellationToken,
        bool completeRequest = false)
    {
        using var scope = _scopes.Create(DbContextScopeOption.ForceCreateNew);
        var state = await _states.GetByNameAsync(schedule.SchedulerStateName, cancellationToken);
        if (state is null)
        {
            _states.Add(new EntityScheduledJobSchedulerState
            {
                Name = schedule.SchedulerStateName,
                LastRunTimeUtc = nowUtc,
                LastEvaluatedAtUtc = nowUtc
            });
        }
        else
        {
            // LastRunTimeUtc is the evaluation baseline, not the time a job executed.
            state.LastRunTimeUtc = nowUtc;
            state.LastEvaluatedAtUtc = nowUtc;
            state.PendingDueAtUtc = null;
            if (completeRequest) { state.RunRequested = false; }
        }

        await scope.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordPendingAsync(
        IScheduledJobDefinition schedule, DateTime dueAtUtc, CancellationToken cancellationToken)
    {
        using var scope = _scopes.Create(DbContextScopeOption.ForceCreateNew);
        var state = await _states.GetByNameAsync(schedule.SchedulerStateName, cancellationToken);
        if (state is null)
        {
            _states.Add(new EntityScheduledJobSchedulerState
            {
                Name = schedule.SchedulerStateName,
                LastRunTimeUtc = dueAtUtc,
                PendingDueAtUtc = dueAtUtc
            });
        }
        else
        {
            state.PendingDueAtUtc = dueAtUtc;
        }

        await scope.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateRegistrations(IReadOnlyList<IScheduledJobDefinition> schedules)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var schedule in schedules)
        {
            if (!IsSafeKey(schedule.Name) || schedule.Name.Length > 64)
            {
                throw new InvalidOperationException(
                    $"Schedule name '{schedule.Name}' must use 1-64 ASCII letters, digits, dots, underscores or hyphens.");
            }

            if (!IsSafeKey(schedule.SchedulerStateName) || schedule.SchedulerStateName.Length > 120)
            {
                throw new InvalidOperationException(
                    $"Schedule '{schedule.Name}' has an invalid checkpoint name '{schedule.SchedulerStateName}'.");
            }

            if (!names.Add(schedule.Name))
            {
                throw new InvalidOperationException($"Duplicate schedule name '{schedule.Name}'.");
            }

            if (!stateNames.Add(schedule.SchedulerStateName))
            {
                throw new InvalidOperationException(
                    $"Duplicate schedule checkpoint name '{schedule.SchedulerStateName}'.");
            }
        }
    }

    private static void ValidateOccurrences(
        IScheduledJobDefinition schedule,
        IReadOnlyList<ScheduledJobOccurrence> occurrences,
        DateTime dueAtUtc)
    {
        if (occurrences is null)
        {
            throw new InvalidOperationException($"Schedule '{schedule.Name}' returned no occurrences.");
        }

        var targetKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var occurrence in occurrences)
        {
            if (occurrences.Count > 1 && string.IsNullOrWhiteSpace(occurrence.TargetKey))
            {
                throw new InvalidOperationException(
                    $"Schedule '{schedule.Name}' must give every fan-out occurrence a stable target key.");
            }

            if (occurrence.TargetKey is not null
                && (!IsSafeKey(occurrence.TargetKey) || occurrence.TargetKey.Length > 32))
            {
                throw new InvalidOperationException(
                    $"Schedule '{schedule.Name}' has an invalid target key '{occurrence.TargetKey}'.");
            }

            if (occurrences.Count > 1 && !targetKeys.Add(occurrence.TargetKey!))
            {
                throw new InvalidOperationException(
                    $"Schedule '{schedule.Name}' has duplicate target key '{occurrence.TargetKey}'.");
            }

            var correlationId = ScheduledJobCorrelationIds.Create(
                schedule.Name, dueAtUtc, occurrence.TargetKey);
            if (correlationId.Length > 128)
            {
                throw new InvalidOperationException(
                    $"Schedule '{schedule.Name}' produces a correlation ID longer than 128 characters.");
            }
        }
    }

    private static bool IsSafeKey(string? value) =>
        !string.IsNullOrEmpty(value)
        && value.All(character =>
            character is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or '.' or '_' or '-');

    private static ScheduledJobRunDto? ToRunDto(EntityJob? job) =>
        job is null
            ? null
            : new ScheduledJobRunDto(
                job.Id, JobService.ToContractStatus(job.Status), job.StartedAtUtc);

    private static ApiResult<RunScheduledJobNowResultDto> PartialRunFailure(
        IScheduledJobDefinition schedule, IReadOnlyList<int> jobIds, string? reason) =>
        new(false, new RunScheduledJobNowResultDto(jobIds.Count, jobIds), 500,
            $"Could not enqueue all jobs for '{schedule.Name}'. {jobIds.Count} job(s) were confirmed saved; "
            + $"another enqueue may also have committed. {reason}");
}
