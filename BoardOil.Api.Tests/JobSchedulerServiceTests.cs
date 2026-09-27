using System.Collections.Concurrent;
using System.Threading.Channels;
using BoardOil.Abstractions.ErrorLogs;
using BoardOil.Abstractions.Jobs;
using BoardOil.Api.Jobs;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.ErrorLogs;
using BoardOil.Contracts.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class JobSchedulerServiceTests
{
    [Fact]
    public async Task RecoveryCompletesBeforeEitherLoopRuns()
    {
        var recoveryStarted = Signal();
        var releaseRecovery = Signal();
        var scheduleCalled = Signal();
        var claimCalled = Signal();
        var runner = new Runner(async token =>
        {
            recoveryStarted.TrySetResult();
            await releaseRecovery.Task.WaitAsync(token);
        }, _ =>
        {
            claimCalled.TrySetResult();
            return Task.FromResult(false);
        });
        var schedule = new Schedule(_ =>
        {
            scheduleCalled.TrySetResult();
            return Task.FromResult<IReadOnlyList<ScheduledJobEnqueueResult>>([]);
        });
        await using var context = Create(runner, schedule);

        await context.Service.StartAsync(TestContext.Current.CancellationToken);
        await recoveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(scheduleCalled.Task.IsCompleted);
        Assert.False(claimCalled.Task.IsCompleted);
        releaseRecovery.SetResult();
        await Task.WhenAll(scheduleCalled.Task, claimCalled.Task).WaitAsync(TimeSpan.FromSeconds(5));
        await context.Service.StopAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, runner.RecoveryCalls);
    }

    [Fact]
    public async Task FailedRecoveryPreventsBothLoops()
    {
        var runner = new Runner(_ => throw new InvalidOperationException("recovery failed"),
            _ => Task.FromResult(false));
        var schedule = new Schedule(_ => Task.FromResult<IReadOnlyList<ScheduledJobEnqueueResult>>([]));
        await using var context = Create(runner, schedule);

        var failure = await Record.ExceptionAsync(() => context.Service.StartAsync(TestContext.Current.CancellationToken));
        if (failure is null)
        {
            failure = await Record.ExceptionAsync(() => context.Service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5)));
        }

        Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal(0, runner.RunCalls);
        Assert.Equal(0, schedule.Calls);
    }

    [Fact]
    public async Task EmptyQueueWaitsTwoSecondsAndShutdownCancelsWaits()
    {
        var runner = new Runner(_ => Task.CompletedTask, _ => Task.FromResult(false));
        var schedule = new Schedule(_ => Task.FromResult<IReadOnlyList<ScheduledJobEnqueueResult>>([]));
        await using var context = Create(runner, schedule);

        await context.Service.StartAsync(TestContext.Current.CancellationToken);
        await context.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(2));
        await context.Clock.WaitForDelayAsync(TimeSpan.FromMinutes(1));
        Assert.Equal(1, runner.RunCalls);
        await context.Service.StopAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(context.Service.ExecuteTask!.IsCompleted);
    }

    [Fact]
    public async Task SchedulingContinuesWhileRunnerIsBlockedAndNeverStartsSecondClaim()
    {
        var handlerStarted = Signal();
        var releaseHandler = Signal();
        var secondEvaluation = Signal();
        var evaluations = 0;
        var runner = new Runner(_ => Task.CompletedTask, async token =>
        {
            handlerStarted.TrySetResult();
            await releaseHandler.Task.WaitAsync(token);
            return true;
        });
        var schedule = new Schedule(_ =>
        {
            if (Interlocked.Increment(ref evaluations) == 2)
            {
                secondEvaluation.TrySetResult();
            }
            return Task.FromResult<IReadOnlyList<ScheduledJobEnqueueResult>>([]);
        });
        await using var context = Create(runner, schedule);

        await context.Service.StartAsync(TestContext.Current.CancellationToken);
        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var timer = await context.Clock.WaitForDelayAsync(TimeSpan.FromMinutes(1));
        timer.Fire();
        await secondEvaluation.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, runner.RunCalls);
        releaseHandler.SetResult();
        await context.Service.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ScheduleFailureBacksOffWhileExecutionContinues()
    {
        var claimCalled = Signal();
        var runner = new Runner(_ => Task.CompletedTask, _ =>
        {
            claimCalled.TrySetResult();
            return Task.FromResult(false);
        });
        var schedule = new Schedule(_ => throw new InvalidOperationException("schedule failed"));
        await using var context = Create(runner, schedule);

        await context.Service.StartAsync(TestContext.Current.CancellationToken);
        await claimCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await context.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, schedule.Calls);
        await context.Service.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ScheduleFailurePersistsJobSchedulerErrorContextBeforeBackoff()
    {
        var failure = new InvalidOperationException("schedule failed");
        var errorLogs = new RecordingErrorLogService();
        var runner = new Runner(_ => Task.CompletedTask, _ => Task.FromResult(false));
        var schedule = new Schedule(_ => throw failure);
        await using var context = Create(runner, schedule, errorLogs: errorLogs);

        await context.Service.StartAsync(TestContext.Current.CancellationToken);
        await context.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(10));
        Assert.Same(failure, errorLogs.Exception);
        Assert.NotNull(errorLogs.Context);
        Assert.Equal(ErrorLogSources.Backend, errorLogs.Context.Source);
        Assert.Equal(ErrorLogAreas.JobScheduler, errorLogs.Context.Area);
        Assert.Contains(nameof(JobSchedulerService), errorLogs.Context.ContextJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnexpectedLoopEscapeCancelsAndObservesSibling()
    {
        var siblingStarted = Signal();
        var siblingStopped = Signal();
        var runner = new Runner(_ => Task.CompletedTask, async token =>
        {
            siblingStarted.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            finally
            {
                siblingStopped.TrySetResult();
            }
            return false;
        });
        var schedule = new Schedule(async token =>
        {
            await siblingStarted.Task.WaitAsync(token);
            throw new InvalidOperationException("schedule failed");
        });
        var clock = new ControlledClock { FailDelay = TimeSpan.FromSeconds(10) };
        await using var context = Create(runner, schedule, clock);

        await context.Service.StartAsync(TestContext.Current.CancellationToken);
        var failure = await Record.ExceptionAsync(() => context.Service.ExecuteTask!
            .WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.IsType<InvalidOperationException>(failure);
        Assert.Contains("timer failed", failure.Message);
        await siblingStopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(context.Service.ExecuteTask!.IsCompleted);
    }

    private static Context Create(
        Runner runner, Schedule schedule, ControlledClock? clock = null, IErrorLogService? errorLogs = null)
    {
        clock ??= new ControlledClock();
        var services = new ServiceCollection()
            .AddSingleton<IJobRunner>(runner)
            .AddSingleton<IScheduledJobService>(schedule);
        if (errorLogs is not null)
        {
            services.AddSingleton(errorLogs);
        }

        var provider = services.BuildServiceProvider();
        var service = new JobSchedulerService(provider.GetRequiredService<IServiceScopeFactory>(),
            clock, NullLogger<JobSchedulerService>.Instance);
        return new Context(provider, service, clock);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed record Context(ServiceProvider Provider, JobSchedulerService Service, ControlledClock Clock) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await Service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception) when (Service.ExecuteTask?.IsFaulted == true)
            {
            }
            Service.Dispose();
            await Provider.DisposeAsync();
        }
    }

    private sealed class Runner(
        Func<CancellationToken, Task> recovery,
        Func<CancellationToken, Task<bool>> run) : IJobRunner
    {
        public int RecoveryCalls { get; private set; }
        public int RunCalls { get; private set; }
        public Task MarkRunningJobsFailedAsync(CancellationToken cancellationToken = default)
        {
            RecoveryCalls++;
            return recovery(cancellationToken);
        }

        public Task<bool> RunNextDueAsync(CancellationToken cancellationToken = default)
        {
            RunCalls++;
            return run(cancellationToken);
        }
    }

    private sealed class Schedule(Func<CancellationToken, Task<IReadOnlyList<ScheduledJobEnqueueResult>>> enqueue)
        : IScheduledJobService
    {
        public int Calls { get; private set; }
        public Task<IReadOnlyList<ScheduledJobEnqueueResult>> EnqueueDueJobsAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return enqueue(cancellationToken);
        }

        public Task<ApiResult<IReadOnlyList<ScheduledJobDto>>> ListAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<ApiResult<RunScheduledJobNowResultDto>> RunNowAsync(
            string scheduleName, int? userId = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingErrorLogService : IErrorLogService
    {
        public Exception? Exception { get; private set; }
        public ErrorLogContext? Context { get; private set; }

        public Task<ApiResult<ErrorLogListDto>> ListAsync(int? offset, int? limit) =>
            throw new NotSupportedException();
        public Task<ApiResult<ErrorLogDetailsDto>> GetAsync(int id) =>
            throw new NotSupportedException();
        public Task<ApiResult<ErrorLogPurgeResultDto>> PurgeExpiredAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ApiResult<ErrorLogDto>> ReportClientErrorAsync(
            ClientErrorReportRequest request, int actorUserId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<int?> LogExceptionAsync(
            Exception exception, ErrorLogContext context, CancellationToken cancellationToken = default)
        {
            Exception = exception;
            Context = context;
            return Task.FromResult<int?>(1);
        }
    }

    private sealed class ControlledClock : TimeProvider
    {
        private readonly Channel<ControlledTimer> _timers = Channel.CreateUnbounded<ControlledTimer>();
        private readonly ConcurrentQueue<ControlledTimer> _unmatched = new();
        public TimeSpan? FailDelay { get; init; }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime == FailDelay)
            {
                throw new InvalidOperationException("timer failed");
            }

            var timer = new ControlledTimer(callback, state, dueTime);
            _timers.Writer.TryWrite(timer);
            return timer;
        }

        public async Task<ControlledTimer> WaitForDelayAsync(TimeSpan delay)
        {
            var pending = new List<ControlledTimer>();
            while (_unmatched.TryDequeue(out var timer))
            {
                if (timer.Delay == delay)
                {
                    foreach (var other in pending) _unmatched.Enqueue(other);
                    return timer;
                }
                pending.Add(timer);
            }
            foreach (var other in pending) _unmatched.Enqueue(other);

            while (true)
            {
                var next = await _timers.Reader.ReadAsync()
                    .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                if (next.Delay == delay) return next;
                _unmatched.Enqueue(next);
            }
        }
    }

    private sealed class ControlledTimer(TimerCallback callback, object? state, TimeSpan delay) : ITimer
    {
        private bool _disposed;
        public TimeSpan Delay { get; } = delay;
        public bool Change(TimeSpan dueTime, TimeSpan period) => !_disposed;
        public void Fire() { if (!_disposed) callback(state); }
        public void Dispose() => _disposed = true;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
