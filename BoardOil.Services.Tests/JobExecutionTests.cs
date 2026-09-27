using System.Data;
using System.Text.Json;
using System.Threading.Channels;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.ErrorLogs;
using BoardOil.Abstractions.Jobs;
using BoardOil.Contracts.Jobs;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using BoardOil.Ef.Scope;
using BoardOil.Services.Jobs;
using BoardOil.Services.DependencyInjection;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class JobExecutionTests : TestBaseDb
{
    private static readonly DateTimeOffset InitialTime = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
    private readonly ManualJobClock _clock = new(InitialTime);
    private readonly SaveFaults _faults = new();
    private readonly RecordingHandler _handler = new();
    private readonly TypedHandler _typedHandler = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddLogging();
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(_clock);
        services.RemoveAll<IDbContextScopeFactory>();
        services.AddSingleton(_faults);
        services.AddTransient<IDbContextScopeFactory>(provider =>
            new FaultingScopeFactory(
                new DbContextScopeFactory(provider.GetRequiredService<IDbContextFactory>()), _faults));
        services.AddSingleton<IJobHandler>(_handler);
        services.AddSingleton<IJobHandler>(_typedHandler);
    }

    [Fact]
    public async Task Enqueue_ShouldPersistNormalisedJobAndLogWithOptionalFields()
    {
        var runAt = InitialTime.AddHours(2).UtcDateTime;
        var created = await ResolveService<IJobService>().EnqueueAsync(
            new CreateJobRequest("  sample  ", "  { \"x\": 1 }  ", runAt, ActorUserId, "  batch-1  "));

        Assert.True(created.Success);
        Assert.Equal(201, created.StatusCode);
        var job = await DbContextForAssert.Jobs.AsNoTracking().Include(x => x.Logs).SingleAsync();
        Assert.Equal("sample", job.Type);
        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Equal(runAt, job.RunAfterUtc);
        Assert.Equal("{ \"x\": 1 }", job.PayloadJson);
        Assert.Equal(ActorUserId, job.UserId);
        Assert.Equal("batch-1", job.CorrelationId);
        Assert.Single(job.Logs);
        Assert.Equal("Job enqueued.", job.Logs[0].Message);
    }

    [Fact]
    public async Task Enqueue_ShouldRollBackHeaderAndLogWhenSaveFailsBeforeCommit()
    {
        _faults.Arm(JobStatus.Pending, 1, afterCommit: false);
        await Assert.ThrowsAsync<InjectedSaveException>(() =>
            ResolveService<IJobService>().EnqueueAsync(new CreateJobRequest("sample")));

        Assert.Empty(await DbContextForAssert.Jobs.AsNoTracking().ToListAsync());
        Assert.Empty(await DbContextForAssert.JobLogs.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData("", null, null)]
    [InlineData("  ", null, null)]
    [InlineData("x", "{broken", null)]
    [InlineData("x", null, "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public async Task Enqueue_ShouldRejectInvalidInputBeforeWrites(
        string type, string? payload, string? correlation)
    {
        var result = await ResolveService<IJobService>().EnqueueAsync(
            new CreateJobRequest(type, payload, CorrelationId: correlation));

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(await DbContextForAssert.Jobs.AsNoTracking().ToListAsync());
        Assert.Empty(await DbContextForAssert.JobLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Enqueue_ShouldRejectOversizedTypeAndDefaultBlankValues()
    {
        var service = ResolveService<IJobService>();
        var rejected = await service.EnqueueAsync(new CreateJobRequest(new string('x', 201)));
        var accepted = await service.EnqueueAsync(new CreateJobRequest("sample", " ", CorrelationId: " "));

        Assert.Equal(400, rejected.StatusCode);
        Assert.True(accepted.Success);
        var job = await DbContextForAssert.Jobs.AsNoTracking().SingleAsync();
        Assert.Equal("{}", job.PayloadJson);
        Assert.Null(job.CorrelationId);
    }

    [Fact]
    public async Task ListGetAndCount_ShouldUsePersistedOrderValidationAndExplicitMappings()
    {
        var service = ResolveService<IJobService>();
        var first = (await service.EnqueueAsync(new CreateJobRequest("sample"))).Data!;
        var second = (await service.EnqueueAsync(new CreateJobRequest("sample"))).Data!;
        var third = (await service.EnqueueAsync(new CreateJobRequest("sample"))).Data!;
        var page = await service.ListAsync(new JobListRequest(1, 1));
        var detail = await service.GetAsync(first.Id);

        Assert.Equal([second.Id], page.Data!.Items.Select(x => x.Id));
        Assert.Equal(3, page.Data.TotalCount);
        Assert.Equal(1, page.Data.Offset);
        Assert.Equal(JobStatuses.Pending, detail.Data!.Status);
        Assert.Equal(JobLogLevels.Info, Assert.Single(detail.Data.Logs).Level);
        Assert.Equal(3, await service.CountActiveAsync());
        Assert.Equal(404, (await service.GetAsync(999999)).StatusCode);
        Assert.Equal(400, (await service.ListAsync(new JobListRequest(-1, 1))).StatusCode);
        Assert.Equal(400, (await service.ListAsync(new JobListRequest(0, 0))).StatusCode);
        Assert.Equal(400, (await service.ListAsync(new JobListRequest(0, 201))).StatusCode);
        Assert.Equal(third.Id, (await service.ListAsync(new JobListRequest())).Data!.Items[0].Id);
    }

    [Fact]
    public void Mapping_ShouldCoverEveryPersistedStatusAndLogLevel()
    {
        Assert.Equal(JobStatuses.Pending, JobService.ToContractStatus(JobStatus.Pending));
        Assert.Equal(JobStatuses.Running, JobService.ToContractStatus(JobStatus.Running));
        Assert.Equal(JobStatuses.Completed, JobService.ToContractStatus(JobStatus.Completed));
        Assert.Equal(JobStatuses.Failed, JobService.ToContractStatus(JobStatus.Failed));
        Assert.Equal(JobStatuses.Cancelled, JobService.ToContractStatus(JobStatus.Cancelled));
        Assert.Equal(JobLogLevels.Info, JobService.ToContractLogLevel(JobLogLevel.Info));
        Assert.Equal(JobLogLevels.Warning, JobService.ToContractLogLevel(JobLogLevel.Warning));
        Assert.Equal(JobLogLevels.Error, JobService.ToContractLogLevel(JobLogLevel.Error));
    }

    [Fact]
    public async Task Get_ShouldMapAllPersistedStatusesAndLogLevelsToStableStrings()
    {
        var statuses = new[]
        {
            JobStatus.Pending, JobStatus.Running, JobStatus.Completed,
            JobStatus.Failed, JobStatus.Cancelled
        };
        foreach (var status in statuses)
        {
            DbContextForArrange.Jobs.Add(new EntityJob
            {
                Type = "sample", Status = status, RunAfterUtc = InitialTime.UtcDateTime,
                Logs =
                [
                    new EntityJobLog { Level = JobLogLevel.Info, Message = "info", LoggedAtUtc = InitialTime.UtcDateTime },
                    new EntityJobLog { Level = JobLogLevel.Warning, Message = "warning", LoggedAtUtc = InitialTime.UtcDateTime },
                    new EntityJobLog { Level = JobLogLevel.Error, Message = "error", LoggedAtUtc = InitialTime.UtcDateTime }
                ]
            });
        }

        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<IJobService>();
        var jobs = await DbContextForAssert.Jobs.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        foreach (var job in jobs)
        {
            var result = await service.GetAsync(job.Id);
            Assert.Equal(JobService.ToContractStatus(job.Status), result.Data!.Status);
            Assert.Equal([JobLogLevels.Info, JobLogLevels.Warning, JobLogLevels.Error],
                result.Data.Logs.Select(x => x.Level));
        }

        Assert.Equal(2, await service.CountActiveAsync());
    }

    [Fact]
    public async Task RunNextDue_ShouldIgnoreFutureAndChooseDueTimeThenId()
    {
        var service = ResolveService<IJobService>();
        await service.EnqueueAsync(new CreateJobRequest("sample", RunAfterUtc: InitialTime.AddHours(1).UtcDateTime));
        Assert.False(await ResolveService<IJobRunner>().RunNextDueAsync());
        Assert.Equal(0, _handler.Calls);
        Assert.Single((await DbContextForAssert.JobLogs.AsNoTracking().ToListAsync()));

        var tie1 = (await service.EnqueueAsync(new CreateJobRequest(
            "sample", RunAfterUtc: InitialTime.AddMinutes(-1).UtcDateTime))).Data!;
        await service.EnqueueAsync(new CreateJobRequest(
            "sample", RunAfterUtc: InitialTime.AddMinutes(-1).UtcDateTime));
        var early = (await service.EnqueueAsync(new CreateJobRequest(
            "sample", RunAfterUtc: InitialTime.AddMinutes(-2).UtcDateTime))).Data!;
        await ResolveService<IJobRunner>().RunNextDueAsync();
        await ResolveService<IJobRunner>().RunNextDueAsync();

        Assert.Equal([early.Id, tie1.Id], _handler.SeenIds);
        Assert.Equal(2, await service.CountActiveAsync());
    }

    [Fact]
    public async Task FailedClaim_ShouldNotInvokeHandlerOrPersistStartLog()
    {
        var id = (await ResolveService<IJobService>().EnqueueAsync(new CreateJobRequest("sample"))).Data!.Id;
        _faults.Arm(JobStatus.Running, 1, afterCommit: false);

        await Assert.ThrowsAsync<InjectedSaveException>(() => ResolveService<IJobRunner>().RunNextDueAsync());
        var job = await DbContextForAssert.Jobs.AsNoTracking().Include(x => x.Logs).SingleAsync(x => x.Id == id);
        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Single(job.Logs);
        Assert.Equal(0, _handler.Calls);
    }

    [Fact]
    public async Task Handler_ShouldObserveCommittedRunningFromIndependentContextAndThenComplete()
    {
        var id = (await ResolveService<IJobService>().EnqueueAsync(new CreateJobRequest("sample"))).Data!.Id;
        _handler.BeforeReturn = async () =>
        {
            Assert.Equal(0, _faults.ActiveWrites);
            await using var db = CreateDbContextForAct();
            var persisted = await db.Jobs.AsNoTracking().Include(x => x.Logs).SingleAsync(x => x.Id == id);
            Assert.Equal(JobStatus.Running, persisted.Status);
            Assert.Equal(2, persisted.Logs.Count);
            Assert.Contains(persisted.Logs, x => x.Message == "Job started.");
        };
        _handler.Result = JobHandlerResult.Succeeded("{\"done\":true}");

        Assert.True(await ResolveService<IJobRunner>().RunNextDueAsync());
        var final = await DbContextForAssert.Jobs.AsNoTracking().Include(x => x.Logs).SingleAsync(x => x.Id == id);
        Assert.Equal(JobStatus.Completed, final.Status);
        Assert.Equal("{\"done\":true}", final.ResultJson);
        Assert.Equal(InitialTime.UtcDateTime, final.CompletedAtUtc);
        Assert.Equal(1, _handler.Calls);
        Assert.Single(final.Logs, x => x.Message == "Job completed.");
        Assert.All(_faults.SaveCallsPerScope, count => Assert.InRange(count, 0, 1));
    }

    [Fact]
    public async Task ReturnedFailureAndUnknownType_ShouldPersistClearDiagnostics()
    {
        _handler.Result = JobHandlerResult.Failed("diagnostic", "{\"reason\":2}");
        var service = ResolveService<IJobService>();
        var failed = (await service.EnqueueAsync(new CreateJobRequest("sample"))).Data!.Id;
        var unknown = (await service.EnqueueAsync(new CreateJobRequest("unregistered"))).Data!.Id;

        await ResolveService<IJobRunner>().RunNextDueAsync();
        await ResolveService<IJobRunner>().RunNextDueAsync();
        var jobs = await DbContextForAssert.Jobs.AsNoTracking().Include(x => x.Logs).ToListAsync();
        Assert.Equal("diagnostic", jobs.Single(x => x.Id == failed).ErrorMessage);
        Assert.Equal("{\"reason\":2}", jobs.Single(x => x.Id == failed).ResultJson);
        Assert.Contains("No job handler", jobs.Single(x => x.Id == unknown).ErrorMessage);
        Assert.All(jobs, x => Assert.Single(x.Logs, log => log.Message == "Job failed."));
        Assert.Equal(1, _handler.Calls);
    }

    [Fact]
    public async Task HandlerException_ShouldPersistFailedAndLinkedCentralError()
    {
        _handler.Throw = new InvalidOperationException("original failure");
        var id = (await ResolveService<IJobService>().EnqueueAsync(new CreateJobRequest("sample"))).Data!.Id;

        Assert.True(await ResolveService<IJobRunner>().RunNextDueAsync());
        var job = await DbContextForAssert.Jobs.AsNoTracking().SingleAsync(x => x.Id == id);
        var error = await DbContextForAssert.ErrorLogs.AsNoTracking().SingleAsync();
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Contains("original failure", job.ErrorMessage);
        Assert.Equal(id, error.JobId);
        Assert.Contains("InvalidOperationException", error.ExceptionType);
        Assert.Contains("original failure", error.Message);
    }

    [Fact]
    public void DuplicateHandlerTypes_ShouldFailDeterministically()
    {
        var handlers = new IJobHandler[] { new RecordingHandler(), new RecordingHandler() };
        var error = Assert.Throws<InvalidOperationException>(() =>
            new JobRunner(null!, null!, null!, handlers, null!, null!, null!, null!));
        Assert.Contains("Duplicate job handler type", error.Message);
    }

    [Fact]
    public async Task UncancelledOperationCanceledException_ShouldBecomeExecutionFailure()
    {
        _handler.Throw = new OperationCanceledException("workflow cancelled itself");
        var id = (await ResolveService<IJobService>().EnqueueAsync(new CreateJobRequest("sample"))).Data!.Id;

        await ResolveService<IJobRunner>().RunNextDueAsync();
        Assert.Equal(JobStatus.Failed,
            (await DbContextForAssert.Jobs.AsNoTracking().SingleAsync(x => x.Id == id)).Status);
        Assert.Equal(id, (await DbContextForAssert.ErrorLogs.AsNoTracking().SingleAsync()).JobId);
    }

    [Fact]
    public async Task HostCancellationDuringHandler_ShouldLeaveRunningForRecovery()
    {
        using var source = new CancellationTokenSource();
        _handler.BeforeReturn = () =>
        {
            source.Cancel();
            throw new OperationCanceledException(source.Token);
        };
        var id = (await ResolveService<IJobService>().EnqueueAsync(new CreateJobRequest("sample"))).Data!.Id;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ResolveService<IJobRunner>().RunNextDueAsync(source.Token));
        Assert.Equal(JobStatus.Running,
            (await DbContextForAssert.Jobs.AsNoTracking().SingleAsync(x => x.Id == id)).Status);
        Assert.Empty(await DbContextForAssert.ErrorLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task TypedPayloadFailure_ShouldNotInvokeWorkflow()
    {
        var id = (await ResolveService<IJobService>().EnqueueAsync(
            new CreateJobRequest("typed", "\"wrong shape\""))).Data!.Id;

        await ResolveService<IJobRunner>().RunNextDueAsync();
        var job = await DbContextForAssert.Jobs.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal("Invalid JSON payload.", job.ErrorMessage);
        Assert.Equal(0, _typedHandler.Calls);
        Assert.Empty(await DbContextForAssert.ErrorLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task WorkflowJsonException_ShouldFollowExecutionExceptionPath()
    {
        _typedHandler.Throw = new JsonException("workflow JSON fault");
        var id = (await ResolveService<IJobService>().EnqueueAsync(
            new CreateJobRequest("typed", "{\"Count\":3}"))).Data!.Id;

        await ResolveService<IJobRunner>().RunNextDueAsync();
        var job = await DbContextForAssert.Jobs.AsNoTracking().SingleAsync(x => x.Id == id);
        var error = await DbContextForAssert.ErrorLogs.AsNoTracking().SingleAsync();
        Assert.Equal(1, _typedHandler.Calls);
        Assert.Contains("workflow JSON fault", job.ErrorMessage);
        Assert.Equal(id, error.JobId);
        Assert.Contains("JsonException", error.ExceptionType);
    }

    [Fact]
    public async Task TerminalSaveFailure_ShouldKeepRunnerPendingAndRetryCapturedOutcome()
    {
        var service = ResolveService<IJobService>();
        var first = (await service.EnqueueAsync(new CreateJobRequest("sample"))).Data!.Id;
        var next = (await service.EnqueueAsync(new CreateJobRequest("sample"))).Data!.Id;
        _handler.Result = JobHandlerResult.Succeeded("{\"original\":true}");
        _faults.Arm(JobStatus.Completed, 1, afterCommit: false);
        var run = ResolveService<IJobRunner>().RunNextDueAsync();
        await _clock.WaitForTimerAsync();

        var waiting = await DbContextForAssert.Jobs.AsNoTracking().Include(x => x.Logs).ToListAsync();
        Assert.False(run.IsCompleted);
        Assert.Equal(1, _handler.Calls);
        Assert.Equal(JobStatus.Running, waiting.Single(x => x.Id == first).Status);
        Assert.DoesNotContain(waiting.Single(x => x.Id == first).Logs, x => x.Message == "Job completed.");
        Assert.Equal(JobStatus.Pending, waiting.Single(x => x.Id == next).Status);
        _clock.Advance(TimeSpan.FromSeconds(10));
        Assert.True(await run.WaitAsync(TimeSpan.FromSeconds(5)));

        var final = await DbContextForAssert.Jobs.AsNoTracking().Include(x => x.Logs).SingleAsync(x => x.Id == first);
        Assert.Equal(JobStatus.Completed, final.Status);
        Assert.Equal("{\"original\":true}", final.ResultJson);
        Assert.Equal(InitialTime.UtcDateTime, final.CompletedAtUtc);
        Assert.Single(final.Logs, x => x.Message == "Job completed.");
        Assert.Equal(1, _handler.Calls);
    }

    [Fact]
    public async Task MultipleTerminalFailures_ShouldUseFreshScopesAndKeepCapturedTime()
    {
        var id = (await ResolveService<IJobService>().EnqueueAsync(new CreateJobRequest("sample"))).Data!.Id;
        _handler.Result = JobHandlerResult.Succeeded("{\"original\":true}");
        _faults.Arm(JobStatus.Completed, 3, afterCommit: false);
        var run = ResolveService<IJobRunner>().RunNextDueAsync();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await _clock.WaitForTimerAsync();
            Assert.False(run.IsCompleted);
            Assert.Equal(JobStatus.Running,
                (await DbContextForAssert.Jobs.AsNoTracking().SingleAsync(x => x.Id == id)).Status);
            _clock.Advance(TimeSpan.FromSeconds(10));
        }

        Assert.True(await run.WaitAsync(TimeSpan.FromSeconds(5)));
        var final = await DbContextForAssert.Jobs.AsNoTracking().Include(x => x.Logs).SingleAsync(x => x.Id == id);
        Assert.Equal(InitialTime.UtcDateTime, final.CompletedAtUtc);
        Assert.Equal("{\"original\":true}", final.ResultJson);
        Assert.Single(final.Logs, x => x.Message == "Job completed.");
        Assert.Equal(1, _handler.Calls);
        Assert.Equal(3, _faults.Failures);
        Assert.True(_faults.WriteScopeCount >= 6);
        Assert.All(_faults.SaveCallsPerScope, count => Assert.InRange(count, 0, 1));
    }

    [Fact]
    public async Task HostCancellationDuringTerminalRetry_ShouldLeaveRunning()
    {
        var id = (await ResolveService<IJobService>().EnqueueAsync(new CreateJobRequest("sample"))).Data!.Id;
        _faults.Arm(JobStatus.Completed, 1, afterCommit: false);
        using var source = new CancellationTokenSource();
        var run = ResolveService<IJobRunner>().RunNextDueAsync(source.Token);
        await _clock.WaitForTimerAsync();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(JobStatus.Running,
            (await DbContextForAssert.Jobs.AsNoTracking().SingleAsync(x => x.Id == id)).Status);
        Assert.Equal(1, _handler.Calls);
    }

    [Fact]
    public async Task CommitSucceededThenThrew_ShouldRecogniseTerminalStateWithoutDuplicateLog()
    {
        var id = (await ResolveService<IJobService>().EnqueueAsync(new CreateJobRequest("sample"))).Data!.Id;
        _faults.Arm(JobStatus.Completed, 1, afterCommit: true);
        var run = ResolveService<IJobRunner>().RunNextDueAsync();
        await _clock.WaitForTimerAsync();
        var committed = await DbContextForAssert.Jobs.AsNoTracking().Include(x => x.Logs).SingleAsync(x => x.Id == id);
        Assert.Equal(JobStatus.Completed, committed.Status);
        Assert.Single(committed.Logs, x => x.Message == "Job completed.");

        _clock.Advance(TimeSpan.FromSeconds(10));
        Assert.True(await run.WaitAsync(TimeSpan.FromSeconds(5)));
        var reloaded = await DbContextForAssert.Jobs.AsNoTracking().Include(x => x.Logs).SingleAsync(x => x.Id == id);
        Assert.Equal(committed.ResultJson, reloaded.ResultJson);
        Assert.Equal(committed.CompletedAtUtc, reloaded.CompletedAtUtc);
        Assert.Single(reloaded.Logs, x => x.Message == "Job completed.");
        Assert.Equal(1, _handler.Calls);
    }

    [Fact]
    public async Task IndependentTerminalStateBeforeRetry_ShouldRemainUntouched()
    {
        var id = (await ResolveService<IJobService>().EnqueueAsync(new CreateJobRequest("sample"))).Data!.Id;
        _faults.Arm(JobStatus.Completed, 1, afterCommit: false);
        var run = ResolveService<IJobRunner>().RunNextDueAsync();
        await _clock.WaitForTimerAsync();
        var independentTime = InitialTime.AddMinutes(2).UtcDateTime;
        await using (var db = CreateDbContextForAct())
        {
            var job = await db.Jobs.SingleAsync(x => x.Id == id);
            job.Status = JobStatus.Cancelled;
            job.ResultJson = "{\"other\":true}";
            job.CompletedAtUtc = independentTime;
            db.JobLogs.Add(new EntityJobLog
            {
                JobId = id, Level = JobLogLevel.Warning,
                Message = "independently terminal", LoggedAtUtc = independentTime
            });
            await db.SaveChangesAsync();
        }

        _clock.Advance(TimeSpan.FromSeconds(10));
        Assert.True(await run.WaitAsync(TimeSpan.FromSeconds(5)));
        var final = await DbContextForAssert.Jobs.AsNoTracking().Include(x => x.Logs).SingleAsync(x => x.Id == id);
        Assert.Equal(JobStatus.Cancelled, final.Status);
        Assert.Equal("{\"other\":true}", final.ResultJson);
        Assert.Equal(independentTime, final.CompletedAtUtc);
        Assert.Single(final.Logs, x => x.Message == "independently terminal");
        Assert.DoesNotContain(final.Logs, x => x.Message == "Job completed.");
    }

    [Fact]
    public async Task Recovery_ShouldOnlyChangeRunningAndRemainIdempotent()
    {
        var pending = new EntityJob { Type = "sample", Status = JobStatus.Pending, RunAfterUtc = InitialTime.UtcDateTime };
        var running1 = new EntityJob { Type = "sample", Status = JobStatus.Running, RunAfterUtc = InitialTime.UtcDateTime };
        var running2 = new EntityJob { Type = "sample", Status = JobStatus.Running, RunAfterUtc = InitialTime.UtcDateTime };
        var complete = new EntityJob { Type = "sample", Status = JobStatus.Completed, RunAfterUtc = InitialTime.UtcDateTime, ResultJson = "{\"keep\":true}" };
        var failed = new EntityJob { Type = "sample", Status = JobStatus.Failed, RunAfterUtc = InitialTime.UtcDateTime, ErrorMessage = "prior failure" };
        var cancelled = new EntityJob { Type = "sample", Status = JobStatus.Cancelled, RunAfterUtc = InitialTime.UtcDateTime, ResultJson = "{\"cancelled\":true}" };
        DbContextForArrange.Jobs.AddRange(pending, running1, running2, complete, failed, cancelled);
        await DbContextForArrange.SaveChangesAsync();
        var runner = ResolveService<IJobRunner>();

        await runner.MarkRunningJobsFailedAsync();
        await runner.MarkRunningJobsFailedAsync();
        var jobs = await DbContextForAssert.Jobs.AsNoTracking().Include(x => x.Logs).ToListAsync();
        Assert.Equal(JobStatus.Pending, jobs.Single(x => x.Id == pending.Id).Status);
        Assert.Equal(JobStatus.Completed, jobs.Single(x => x.Id == complete.Id).Status);
        Assert.Equal("{\"keep\":true}", jobs.Single(x => x.Id == complete.Id).ResultJson);
        Assert.Equal("prior failure", jobs.Single(x => x.Id == failed.Id).ErrorMessage);
        Assert.Empty(jobs.Single(x => x.Id == failed.Id).Logs);
        Assert.Equal("{\"cancelled\":true}", jobs.Single(x => x.Id == cancelled.Id).ResultJson);
        Assert.Empty(jobs.Single(x => x.Id == cancelled.Id).Logs);
        foreach (var id in new[] { running1.Id, running2.Id })
        {
            var job = jobs.Single(x => x.Id == id);
            Assert.Equal(JobStatus.Failed, job.Status);
            Assert.Equal(InitialTime.UtcDateTime, job.CompletedAtUtc);
            Assert.Contains("restart", job.ErrorMessage);
            Assert.Single(job.Logs, x => x.Message.Contains("restart"));
        }

        Assert.True(await runner.RunNextDueAsync());
        Assert.Equal([pending.Id], _handler.SeenIds);
    }

    [Fact]
    public async Task RecoveryAndClaim_ShouldShareProcessGate()
    {
        DbContextForArrange.Jobs.AddRange(
            new EntityJob { Type = "sample", Status = JobStatus.Running, RunAfterUtc = InitialTime.UtcDateTime },
            new EntityJob { Type = "sample", Status = JobStatus.Pending, RunAfterUtc = InitialTime.UtcDateTime });
        await DbContextForArrange.SaveChangesAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _faults.BeforeSaveAsync = async db =>
        {
            if (db.ChangeTracker.Entries<EntityJob>().Any(x => x.Entity.Status == JobStatus.Failed))
            {
                entered.TrySetResult();
                await release.Task;
            }
        };
        var runner = ResolveService<IJobRunner>();
        var recovery = runner.MarkRunningJobsFailedAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var run = runner.RunNextDueAsync();
        Assert.False(run.IsCompleted);
        Assert.Equal(0, _handler.Calls);
        release.TrySetResult();

        await recovery.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(await run.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, _handler.Calls);
    }

    [Fact]
    public async Task ErrorLogContext_ShouldExposeJobIdAndClientReportsCannotChooseIt()
    {
        var id = (await ResolveService<IJobService>().EnqueueAsync(new CreateJobRequest("sample"))).Data!.Id;
        var errors = ResolveService<IErrorLogService>();
        var errorId = await errors.LogExceptionAsync(
            new InvalidOperationException("linked"),
            new ErrorLogContext(ErrorLogSources.Backend, ErrorLogAreas.JobScheduler, JobId: id));
        var details = await errors.GetAsync(errorId!.Value);
        var list = await errors.ListAsync(0, 10);

        Assert.Equal(id, details.Data!.JobId);
        Assert.Equal(id, Assert.Single(list.Data!.Items).JobId);
        Assert.Equal(id, (await DbContextForAssert.ErrorLogs.AsNoTracking().SingleAsync()).JobId);
    }

    [Fact]
    public void ServiceRegistration_ShouldResolveRunnerWithMaintenanceHandlersAndWithoutHostedWorker()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBoardOilServices();
        BoardOil.Ef.DependencyInjection.ServiceCollectionExtensions.AddBoardOilEfInfrastructure(
            services, "DataSource=:memory:");
        Assert.Equal(3, services.Count(x => x.ServiceType == typeof(IJobHandler)));
        Assert.DoesNotContain(services, x => x.ServiceType.FullName == "Microsoft.Extensions.Hosting.IHostedService");
        Assert.Contains(services, x => x.ServiceType == typeof(IJobRunner));
        Assert.Contains(services, x => x.ServiceType == typeof(IJobService));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IJobRunner>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IJobService>());
    }

    private sealed class RecordingHandler : IJobHandler
    {
        public string Type => "sample";
        public int Calls { get; private set; }
        public List<int> SeenIds { get; } = [];
        public Func<Task>? BeforeReturn { get; set; }
        public JobHandlerResult Result { get; set; } = JobHandlerResult.Succeeded();
        public Exception? Throw { get; set; }

        public async Task<JobHandlerResult> HandleAsync(JobContext context, CancellationToken cancellationToken)
        {
            Calls++;
            SeenIds.Add(context.JobId);
            if (BeforeReturn is not null)
            {
                await BeforeReturn();
            }

            if (Throw is not null)
            {
                throw Throw;
            }

            return Result;
        }
    }

    private sealed record TypedPayload(int Count);

    private sealed class TypedHandler : JobHandlerBase<TypedPayload>
    {
        public override string Type => "typed";
        public int Calls { get; private set; }
        public Exception? Throw { get; set; }

        protected override Task<JobHandlerResult> HandleTypedAsync(
            JobContext context, TypedPayload payload, CancellationToken cancellationToken)
        {
            Calls++;
            if (Throw is not null)
            {
                throw Throw;
            }

            return Task.FromResult(JobHandlerResult.Succeeded());
        }
    }

    private sealed class SaveFaults
    {
        private readonly List<int> _saveCallsPerScope = [];
        public IReadOnlyList<int> SaveCallsPerScope => _saveCallsPerScope;
        public int WriteScopeCount => _saveCallsPerScope.Count;
        public int ActiveWrites { get; private set; }
        public JobStatus? Target { get; private set; }
        public int Remaining { get; private set; }
        public bool AfterCommit { get; private set; }
        public TaskCompletionSource<int> FailureSignal { get; private set; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Failures { get; private set; }
        public Func<BoardOilDbContext, Task>? BeforeSaveAsync { get; set; }

        public void Arm(JobStatus target, int count, bool afterCommit)
        {
            Target = target;
            Remaining = count;
            AfterCommit = afterCommit;
            Failures = 0;
            FailureSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public int RegisterScope()
        {
            _saveCallsPerScope.Add(0);
            ActiveWrites++;
            return _saveCallsPerScope.Count - 1;
        }

        public void DisposeScope() => ActiveWrites--;

        public void RecordSave(int index) => _saveCallsPerScope[index]++;

        public bool ShouldFail(BoardOilDbContext db)
        {
            if (Remaining == 0 || Target is null ||
                !db.ChangeTracker.Entries<EntityJob>().Any(x => x.Entity.Status == Target))
            {
                return false;
            }

            Remaining--;
            Failures++;
            FailureSignal.TrySetResult(Failures);
            return true;
        }
    }

    private sealed class InjectedSaveException : Exception;

    private sealed class FaultingScopeFactory(IDbContextScopeFactory inner, SaveFaults faults) : IDbContextScopeFactory
    {
        public IDbContextScope Create(DbContextScopeOption joiningOption = DbContextScopeOption.JoinExisting) =>
            new FaultingScope(inner.Create(joiningOption), faults);
        public IDbContextReadOnlyScope CreateReadOnly(DbContextScopeOption joiningOption = DbContextScopeOption.JoinExisting) =>
            inner.CreateReadOnly(joiningOption);
        public IDbContextScope CreateWithTransaction(IsolationLevel isolationLevel) =>
            new FaultingScope(inner.CreateWithTransaction(isolationLevel), faults);
        public IDbContextReadOnlyScope CreateReadOnlyWithTransaction(IsolationLevel isolationLevel) =>
            inner.CreateReadOnlyWithTransaction(isolationLevel);
        public IDisposable SuppressAmbientContext() => inner.SuppressAmbientContext();
    }

    private sealed class FaultingScope(IDbContextScope inner, SaveFaults faults) : IDbContextScope
    {
        private readonly int _index = faults.RegisterScope();
        public IDbContextCollection DbContexts => inner.DbContexts;
        public void Dispose()
        {
            inner.Dispose();
            faults.DisposeScope();
        }
        public Task Transaction(Func<IDbContextTransactionScope, IDbContextTransaction, Task> executor) =>
            inner.Transaction(executor);

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            faults.RecordSave(_index);
            var db = DbContexts.Get<BoardOilDbContext>();
            if (faults.BeforeSaveAsync is not null)
            {
                await faults.BeforeSaveAsync(db);
            }

            var fail = faults.ShouldFail(db);
            if (fail && !faults.AfterCommit)
            {
                throw new InjectedSaveException();
            }

            var saved = await inner.SaveChangesAsync(cancellationToken);
            if (fail)
            {
                throw new InjectedSaveException();
            }

            return saved;
        }
    }

    private sealed class ManualJobClock(DateTimeOffset initial) : TimeProvider
    {
        private readonly List<ManualTimer> _timers = [];
        private readonly Channel<bool> _created = Channel.CreateUnbounded<bool>();
        private DateTimeOffset _now = initial;
        public override DateTimeOffset GetUtcNow() => _now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state, _now + dueTime);
            _timers.Add(timer);
            _created.Writer.TryWrite(true);
            return timer;
        }

        public async Task WaitForTimerAsync() =>
            await _created.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        public void Advance(TimeSpan amount)
        {
            _now += amount;
            foreach (var timer in _timers.ToArray())
            {
                timer.FireIfDue(_now);
            }
        }

        private sealed class ManualTimer(
            ManualJobClock clock, TimerCallback callback, object? state, DateTimeOffset due) : ITimer
        {
            private DateTimeOffset _due = due;
            private bool _disposed;
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                _due = clock._now + dueTime;
                return !_disposed;
            }

            public void FireIfDue(DateTimeOffset now)
            {
                if (!_disposed && now >= _due)
                {
                    _disposed = true;
                    callback(state);
                }
            }

            public void Dispose() => _disposed = true;
            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
