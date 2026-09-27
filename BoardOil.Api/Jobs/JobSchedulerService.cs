using System.Text.Json;
using BoardOil.Abstractions.ErrorLogs;
using BoardOil.Abstractions.Jobs;

namespace BoardOil.Api.Jobs;

public sealed class JobSchedulerService(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<JobSchedulerService> logger) : BackgroundService
{
    private static readonly TimeSpan ScheduleDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Neither loop may touch the queue before interrupted claims are terminal.
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IJobRunner>()
                .MarkRunningJobsFailedAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Job startup recovery failed.");
            await LogFailureAsync(exception, CancellationToken.None);
            throw;
        }

        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var scheduleTask = ScheduleLoopAsync(lifetime.Token);
        var executionTask = ExecutionLoopAsync(lifetime.Token);
        try
        {
            // If a loop escapes its operation guard, stop and observe its sibling.
            await Task.WhenAny(scheduleTask, executionTask);
        }
        finally
        {
            try
            {
                await lifetime.CancelAsync();
            }
            finally
            {
                try
                {
                    await Task.WhenAll(scheduleTask, executionTask);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                }
            }
        }

        if (!stoppingToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("A job scheduler loop stopped unexpectedly.");
        }
    }

    private async Task ScheduleLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using (var scope = scopeFactory.CreateAsyncScope())
                {
                    var scheduler = scope.ServiceProvider.GetRequiredService<IScheduledJobService>();
                    var results = await scheduler.EnqueueDueJobsAsync(cancellationToken);
                    var count = results.Count(result => result.Enqueued);
                    if (count > 0)
                    {
                        logger.LogInformation("Enqueued {JobCount} scheduled job(s).", count);
                    }
                }

                await Task.Delay(ScheduleDelay, clock, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                await HandleLoopFailureAsync(exception, "schedule evaluation", cancellationToken);
            }
        }
    }

    private async Task ExecutionLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                bool ranJob;
                await using (var scope = scopeFactory.CreateAsyncScope())
                {
                    var runner = scope.ServiceProvider.GetRequiredService<IJobRunner>();
                    ranJob = await runner.RunNextDueAsync(cancellationToken);
                }

                if (!ranJob)
                {
                    await Task.Delay(IdleDelay, clock, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                await HandleLoopFailureAsync(exception, "job execution", cancellationToken);
            }
        }
    }

    private async Task HandleLoopFailureAsync(
        Exception exception, string operation, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Job {Operation} failed.", operation);
        await LogFailureAsync(exception, cancellationToken);
        try
        {
            await Task.Delay(ErrorDelay, clock, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task LogFailureAsync(Exception exception, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IErrorLogService>().LogExceptionAsync(
                exception,
                new ErrorLogContext(ErrorLogSources.Backend, ErrorLogAreas.JobScheduler,
                    ContextJson: JsonSerializer.Serialize(new { service = nameof(JobSchedulerService) })),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception logException)
        {
            logger.LogError(logException, "Could not persist job scheduler failure.");
        }
    }
}
