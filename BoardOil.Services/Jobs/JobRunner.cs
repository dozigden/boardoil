using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.ErrorLogs;
using BoardOil.Abstractions.Jobs;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Jobs;
using Microsoft.Extensions.Logging;

namespace BoardOil.Services.Jobs;

public sealed class JobRunner(
    IDbContextScopeFactory scopes,
    IJobRepository jobs,
    IJobLogRepository logs,
    IEnumerable<IJobHandler> handlers,
    IErrorLogService errorLogs,
    JobLifecycleGate lifecycleGate,
    TimeProvider clock,
    ILogger<JobRunner> logger) : IJobRunner
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);
    private readonly IReadOnlyDictionary<string, IJobHandler> _handlers = BuildHandlerMap(handlers);

    public async Task<bool> RunNextDueAsync(CancellationToken cancellationToken = default)
    {
        // Handler services must commit their own work before the job becomes Completed.
        using var ambientContext = scopes.SuppressAmbientContext();
        var started = await lifecycleGate.ExecuteAsync(
            token => ClaimNextDueAsync(token), cancellationToken);
        if (started is null)
        {
            return false;
        }

        var context = started.Context;
        JobHandlerResult result;
        try
        {
            if (!_handlers.TryGetValue(context.Type, out var handler))
            {
                result = JobHandlerResult.Failed($"No job handler registered for type '{context.Type}'.");
            }
            else
            {
                result = await handler.HandleAsync(context, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Job {JobId} ({JobType}) failed during execution.", context.JobId, context.Type);
            await errorLogs.LogExceptionAsync(
                exception,
                new ErrorLogContext(ErrorLogSources.Backend, ErrorLogAreas.JobRunner,
                    JobId: context.JobId,
                    ContextJson: JsonSerializer.Serialize(new { context.JobId, context.Type })),
                cancellationToken);
            result = JobHandlerResult.Failed(exception.Message);
        }

        var outcome = new TerminalOutcome(
            context.JobId,
            result.Success ? JobStatus.Completed : JobStatus.Failed,
            NormaliseResultJson(result.ResultJson),
            result.Success ? null : result.ErrorMessage ?? "Job failed.",
            clock.GetUtcNow().UtcDateTime);
        await PersistOutcomeUntilSavedAsync(outcome, cancellationToken);
        return true;
    }

    public Task MarkRunningJobsFailedAsync(CancellationToken cancellationToken = default) =>
        lifecycleGate.ExecuteAsync(async token =>
        {
            using var scope = scopes.Create(DbContextScopeOption.ForceCreateNew);
            var running = await jobs.ListRunningForUpdateAsync(token);
            if (running.Count == 0)
            {
                return;
            }

            var now = clock.GetUtcNow().UtcDateTime;
            foreach (var job in running)
            {
                job.Status = JobStatus.Failed;
                job.ErrorMessage = "Job interrupted by application restart.";
                job.CompletedAtUtc = now;
                logs.Add(new EntityJobLog
                {
                    JobId = job.Id,
                    Level = JobLogLevel.Error,
                    Message = "Job interrupted by application restart.",
                    LoggedAtUtc = now
                });
            }

            await scope.SaveChangesAsync(token);
        }, cancellationToken);

    private async Task<StartedJob?> ClaimNextDueAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.Create(DbContextScopeOption.ForceCreateNew);
        var now = clock.GetUtcNow().UtcDateTime;
        var job = await jobs.FindNextDueAsync(now, cancellationToken);
        if (job is null)
        {
            return null;
        }

        job.Status = JobStatus.Running;
        job.StartedAtUtc = now;
        job.ErrorMessage = null;
        logs.Add(new EntityJobLog
        {
            JobId = job.Id,
            Level = JobLogLevel.Info,
            Message = "Job started.",
            DataJson = JsonSerializer.Serialize(new { job.Id, job.Type }),
            LoggedAtUtc = now
        });
        await scope.SaveChangesAsync(cancellationToken);
        return new StartedJob(new JobContext(job.Id, job.Type, job.PayloadJson));
    }

    private async Task PersistOutcomeUntilSavedAsync(
        TerminalOutcome outcome, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var scope = scopes.Create(DbContextScopeOption.ForceCreateNew);
                var job = await jobs.GetForUpdateAsync(outcome.JobId, cancellationToken);
                if (job is null || job.Status != JobStatus.Running)
                {
                    // A prior commit may have succeeded despite the caller seeing an exception,
                    // or recovery/another actor may already have made the job terminal.
                    return;
                }

                job.Status = outcome.Status;
                job.ResultJson = outcome.ResultJson;
                job.ErrorMessage = outcome.ErrorMessage;
                job.CompletedAtUtc = outcome.CompletedAtUtc;
                logs.Add(new EntityJobLog
                {
                    JobId = outcome.JobId,
                    Level = outcome.Status == JobStatus.Completed ? JobLogLevel.Info : JobLogLevel.Error,
                    Message = outcome.Status == JobStatus.Completed ? "Job completed." : "Job failed.",
                    DataJson = outcome.Status == JobStatus.Completed
                        ? outcome.ResultJson
                        : JsonSerializer.Serialize(new { errorMessage = outcome.ErrorMessage }),
                    LoggedAtUtc = outcome.CompletedAtUtc
                });
                await scope.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not persist terminal outcome for job {JobId}; retrying.", outcome.JobId);
                await Task.Delay(RetryDelay, clock, cancellationToken);
            }
        }
    }

    private static IReadOnlyDictionary<string, IJobHandler> BuildHandlerMap(IEnumerable<IJobHandler> handlers)
    {
        var map = new Dictionary<string, IJobHandler>(StringComparer.Ordinal);
        foreach (var handler in handlers)
        {
            if (string.IsNullOrWhiteSpace(handler.Type))
            {
                throw new InvalidOperationException("A job handler type is required.");
            }

            if (!map.TryAdd(handler.Type, handler))
            {
                throw new InvalidOperationException($"Duplicate job handler type '{handler.Type}'.");
            }
        }

        return map;
    }

    private static string NormaliseResultJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "{}";
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return json.Trim();
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(new { message = json });
        }
    }

    private sealed record StartedJob(JobContext Context);
    private sealed record TerminalOutcome(
        int JobId, JobStatus Status, string ResultJson, string? ErrorMessage, DateTime CompletedAtUtc);
}
