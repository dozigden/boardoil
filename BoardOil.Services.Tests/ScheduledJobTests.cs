using System.Data;
using BoardOil.Abstractions.Configuration;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Jobs;
using BoardOil.Contracts.Configuration;
using BoardOil.Contracts.Jobs;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using BoardOil.Ef.Scope;
using BoardOil.Services.Jobs;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class ScheduledJobTests : TestBaseDb
{
    private static readonly DateTime Start = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestClock _clock = new(Start);
    private readonly List<IScheduledJobDefinition> _definitions = [];
    private readonly SaveFaults _faults = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(_clock);
        services.AddSingleton(_faults);
        services.RemoveAll<IDbContextScopeFactory>();
        services.AddTransient<IDbContextScopeFactory>(provider =>
            new FaultingScopeFactory(
                new DbContextScopeFactory(provider.GetRequiredService<IDbContextFactory>()), _faults));
        services.AddSingleton<IEnumerable<IScheduledJobDefinition>>(_definitions);
    }

    [Fact]
    public async Task FirstEvaluation_ShouldBaselineOrdinaryAndEnqueueInitialOnly()
    {
        _definitions.Add(new TestDefinition("ordinary", false));
        _definitions.Add(new TestDefinition("initial", true));

        var result = await ResolveService<IScheduledJobService>().EnqueueDueJobsAsync();

        Assert.Single(result);
        Assert.Equal("scheduled:initial:20260926T120000Z", result[0].CorrelationId);
        Assert.Equal(Start, result[0].DueAtUtc);
        var states = await DbContextForAssert.ScheduledJobSchedulerStates.AsNoTracking().ToListAsync();
        Assert.Equal(2, states.Count);
        Assert.All(states, state =>
        {
            Assert.Equal(Start, state.LastRunTimeUtc);
            Assert.Equal(Start, state.LastEvaluatedAtUtc);
            Assert.Null(state.PendingDueAtUtc);
        });
        Assert.Single(await DbContextForAssert.Jobs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task InitialCheckpointFailure_ShouldRetryOriginalIdentityAfterClockAdvances()
    {
        _definitions.Add(new TestDefinition("initial", true));
        _faults.FailNextFinalCheckpoint();
        var service = ResolveService<IScheduledJobService>();

        await Assert.ThrowsAsync<AggregateException>(() => service.EnqueueDueJobsAsync());
        var pending = await DbContextForAssert.ScheduledJobSchedulerStates.AsNoTracking().SingleAsync();
        Assert.Equal(Start, pending.PendingDueAtUtc);
        Assert.Null(pending.LastEvaluatedAtUtc);
        _clock.Set(Start.AddHours(2));
        var retry = await service.EnqueueDueJobsAsync();

        Assert.Single(retry);
        Assert.False(retry[0].Enqueued);
        Assert.Equal("scheduled:initial:20260926T120000Z", retry[0].CorrelationId);
        Assert.Single(await DbContextForAssert.Jobs.AsNoTracking().ToListAsync());
        var completed = await DbContextForAssert.ScheduledJobSchedulerStates.AsNoTracking().SingleAsync();
        Assert.Equal(Start.AddHours(2), completed.LastRunTimeUtc);
        Assert.Equal(Start.AddHours(2), completed.LastEvaluatedAtUtc);
        Assert.Null(completed.PendingDueAtUtc);
    }

    [Fact]
    public async Task EnqueueSaveWithLostAcknowledgement_ShouldRetryWithoutDuplicate()
    {
        _definitions.Add(new TestDefinition("initial", true));
        _faults.FailNextJobAfterCommit();
        var service = ResolveService<IScheduledJobService>();

        await Assert.ThrowsAsync<AggregateException>(() => service.EnqueueDueJobsAsync());
        Assert.Single(await DbContextForAssert.Jobs.AsNoTracking().ToListAsync());
        _clock.Set(Start.AddHours(1));
        var retry = await service.EnqueueDueJobsAsync();

        Assert.False(Assert.Single(retry).Enqueued);
        Assert.Single(await DbContextForAssert.Jobs.AsNoTracking().ToListAsync());
        Assert.Null((await DbContextForAssert.ScheduledJobSchedulerStates.AsNoTracking().SingleAsync())
            .PendingDueAtUtc);
    }

    [Fact]
    public async Task FanOutFailure_ShouldFinishOriginalOccurrenceAfterLaterBoundary()
    {
        var definition = new TestDefinition("fan", false)
        {
            Cron = "0 * * * *",
            Occurrences =
            [
                new ScheduledJobOccurrence("maintenance", "{}", "first"),
                new ScheduledJobOccurrence("maintenance", "{broken", "second")
            ]
        };
        _definitions.Add(definition);
        var service = ResolveService<IScheduledJobService>();
        await service.EnqueueDueJobsAsync();
        _clock.Set(Start.AddHours(1));

        var error = await Assert.ThrowsAsync<AggregateException>(() => service.EnqueueDueJobsAsync());
        Assert.Contains("fan", error.InnerExceptions[0].Message);
        var pending = await DbContextForAssert.ScheduledJobSchedulerStates.AsNoTracking().SingleAsync();
        Assert.Equal(Start, pending.LastRunTimeUtc);
        Assert.Equal(Start, pending.LastEvaluatedAtUtc);
        Assert.Equal(Start.AddHours(1), pending.PendingDueAtUtc);
        definition.Occurrences =
        [
            new ScheduledJobOccurrence("maintenance", "{}", "first"),
            new ScheduledJobOccurrence("maintenance", "{}", "second")
        ];
        _clock.Set(Start.AddHours(3));

        var retry = await service.EnqueueDueJobsAsync();

        Assert.Equal(2, retry.Count);
        Assert.Single(retry, x => x.Enqueued);
        Assert.All(retry, x => Assert.Equal(Start.AddHours(1), x.DueAtUtc));
        Assert.Equal(
            ["scheduled:fan:first:20260926T130000Z", "scheduled:fan:second:20260926T130000Z"],
            (await DbContextForAssert.Jobs.AsNoTracking().OrderBy(x => x.Id).ToListAsync())
                .Select(x => x.CorrelationId));
        Assert.Equal(Start.AddHours(3),
            (await DbContextForAssert.ScheduledJobSchedulerStates.AsNoTracking().SingleAsync()).LastRunTimeUtc);
    }

    [Fact]
    public async Task DueBoundaryAndCatchUp_ShouldUseLatestOccurrenceTime()
    {
        _definitions.Add(new TestDefinition("hourly", false) { Cron = "0 * * * *" });
        var service = ResolveService<IScheduledJobService>();
        await service.EnqueueDueJobsAsync();
        _clock.Set(Start.AddMinutes(59));
        Assert.Empty(await service.EnqueueDueJobsAsync());
        _clock.Set(Start.AddHours(1));
        var onBoundary = await service.EnqueueDueJobsAsync();
        _clock.Set(Start.AddHours(4).AddMinutes(30));
        var catchUp = await service.EnqueueDueJobsAsync();

        Assert.Equal(Start.AddHours(1), Assert.Single(onBoundary).DueAtUtc);
        Assert.Equal(Start.AddHours(4), Assert.Single(catchUp).DueAtUtc);
        var jobs = await DbContextForAssert.Jobs.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        Assert.Equal([Start.AddHours(1), Start.AddHours(4)], jobs.Select(x => x.RunAfterUtc));
        Assert.Equal(
            ["scheduled:hourly:20260926T130000Z", "scheduled:hourly:20260926T160000Z"],
            jobs.Select(x => x.CorrelationId));
    }

    [Fact]
    public async Task DisabledSchedule_ShouldAdvanceWithoutJobsThenResumeFromNewBaseline()
    {
        var definition = new TestDefinition("toggle", false) { Cron = "0 * * * *", Enabled = false };
        _definitions.Add(definition);
        var service = ResolveService<IScheduledJobService>();
        await service.EnqueueDueJobsAsync();
        _clock.Set(Start.AddHours(3));
        await service.EnqueueDueJobsAsync();
        definition.Enabled = true;
        _clock.Set(Start.AddHours(4));
        var result = await service.EnqueueDueJobsAsync();

        Assert.Equal(Start.AddHours(4), Assert.Single(result).DueAtUtc);
        Assert.Single(await DbContextForAssert.Jobs.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData(JobStatus.Pending)]
    [InlineData(JobStatus.Running)]
    [InlineData(JobStatus.Completed)]
    [InlineData(JobStatus.Failed)]
    [InlineData(JobStatus.Cancelled)]
    public async Task Retry_ShouldSuppressPersistedOccurrenceRegardlessOfStatus(JobStatus status)
    {
        _definitions.Add(new TestDefinition("daily", false) { Cron = "0 * * * *" });
        DbContextForArrange.ScheduledJobSchedulerStates.Add(new EntityScheduledJobSchedulerState
        {
            Name = "daily-state", LastRunTimeUtc = Start, LastEvaluatedAtUtc = Start,
            PendingDueAtUtc = Start.AddHours(1)
        });
        DbContextForArrange.Jobs.Add(new EntityJob
        {
            Type = "maintenance", Status = status, RunAfterUtc = Start.AddHours(1),
            CorrelationId = "scheduled:daily:20260926T130000Z"
        });
        await DbContextForArrange.SaveChangesAsync();
        _clock.Set(Start.AddHours(2));

        var result = await ResolveService<IScheduledJobService>().EnqueueDueJobsAsync();

        Assert.False(Assert.Single(result).Enqueued);
        Assert.Single(await DbContextForAssert.Jobs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task BrokenDefinition_ShouldLeaveItsCheckpointAndAdvanceHealthyDefinition()
    {
        _definitions.Add(new TestDefinition("broken", false) { Cron = "not cron" });
        _definitions.Add(new TestDefinition("healthy", false));

        var error = await Assert.ThrowsAsync<AggregateException>(() =>
            ResolveService<IScheduledJobService>().EnqueueDueJobsAsync());

        Assert.Contains("broken", error.InnerExceptions.Single().Message);
        Assert.Equal("healthy-state",
            (await DbContextForAssert.ScheduledJobSchedulerStates.AsNoTracking().SingleAsync()).Name);
    }

    [Fact]
    public async Task TimeZoneChange_ShouldAtomicallyResetBaselinesAndPreservePendingAndEvaluation()
    {
        var oldEvaluation = Start.AddDays(-1);
        DbContextForArrange.ScheduledJobSchedulerStates.AddRange(
            new EntityScheduledJobSchedulerState
            {
                Name = "one", LastRunTimeUtc = oldEvaluation, LastEvaluatedAtUtc = oldEvaluation,
                PendingDueAtUtc = oldEvaluation.AddHours(1)
            },
            new EntityScheduledJobSchedulerState
            {
                Name = "two", LastRunTimeUtc = oldEvaluation, LastEvaluatedAtUtc = oldEvaluation
            });
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ISystemTimeZoneService>();
        _faults.FailNextTimeZoneSave();

        await Assert.ThrowsAsync<InjectedSaveException>(() =>
            service.UpdateAsync(new UpdateSystemTimeZoneRequest("Europe/London")));
        Assert.Equal("UTC", (await service.GetAsync()).Data!.SystemTimeZoneId);
        Assert.All(await DbContextForAssert.ScheduledJobSchedulerStates.AsNoTracking().ToListAsync(),
            state => Assert.Equal(oldEvaluation, state.LastRunTimeUtc));

        var changed = await service.UpdateAsync(new UpdateSystemTimeZoneRequest("Europe/London"));

        Assert.Equal("Europe/London", changed.Data!.SystemTimeZoneId);
        var states = await DbContextForAssert.ScheduledJobSchedulerStates.AsNoTracking()
            .OrderBy(x => x.Name).ToListAsync();
        Assert.All(states, state =>
        {
            Assert.Equal(Start, state.LastRunTimeUtc);
            Assert.Equal(oldEvaluation, state.LastEvaluatedAtUtc);
        });
        Assert.Equal(oldEvaluation.AddHours(1), states[0].PendingDueAtUtc);
        Assert.Null(states[1].PendingDueAtUtc);
    }

    [Fact]
    public async Task TimeZoneAndEvaluation_ShouldTakeClockAfterSharedGate()
    {
        _definitions.Add(new TestDefinition("daily", false));
        var gate = ResolveService<SchedulingGate>();
        using var held = await gate.EnterAsync(CancellationToken.None);
        var scheduled = ResolveService<IScheduledJobService>().EnqueueDueJobsAsync();
        var changed = ResolveService<ISystemTimeZoneService>()
            .UpdateAsync(new UpdateSystemTimeZoneRequest("Europe/London"));
        _clock.Set(Start.AddHours(2));
        Assert.False(scheduled.IsCompleted);
        Assert.False(changed.IsCompleted);
        held.Dispose();

        await Task.WhenAll(scheduled, changed).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(Start.AddHours(2),
            (await DbContextForAssert.ScheduledJobSchedulerStates.AsNoTracking().SingleAsync()).LastRunTimeUtc);
        Assert.Equal("Europe/London",
            (await ResolveService<ISystemTimeZoneService>().GetAsync()).Data!.SystemTimeZoneId);
    }

    [Fact]
    public async Task OverlappingEvaluations_ShouldSerialiseBeforeReadingCheckpoint()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        _definitions.Add(new TestDefinition("overlap", true)
        {
            BeforeConfiguration = async () =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    entered.SetResult();
                    await release.Task;
                }
            }
        });
        var first = ResolveService<IScheduledJobService>().EnqueueDueJobsAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task<IReadOnlyList<ScheduledJobEnqueueResult>> second;
        try
        {
            second = ResolveService<IScheduledJobService>().EnqueueDueJobsAsync();
            Assert.False(second.IsCompleted);
            Assert.Equal(1, Volatile.Read(ref calls));
        }
        finally
        {
            release.TrySetResult();
        }

        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(await DbContextForAssert.Jobs.AsNoTracking().ToListAsync());
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ConfiguredTimeZone_ShouldAffectNextOccurrenceAndFallbackFromInvalidStoredValue()
    {
        _definitions.Add(new TestDefinition("local", false) { Cron = "0 9 * * *" });
        var zones = ResolveService<ISystemTimeZoneService>();
        Assert.Equal(400, (await zones.UpdateAsync(new UpdateSystemTimeZoneRequest("Invalid/Zone"))).StatusCode);
        Assert.Null(zones.Validate("Europe/London"));
        await zones.UpdateAsync(new UpdateSystemTimeZoneRequest("Europe/London"));
        var list = await ResolveService<IScheduledJobService>().ListAsync();

        Assert.Equal("Europe/London", Assert.Single(list.Data!).TimeZoneId);
        Assert.Equal(new DateTime(2026, 9, 27, 8, 0, 0, DateTimeKind.Utc),
            Assert.Single(list.Data!).NextOccurrenceUtc);

        var setting = await DbContextForArrange.AppSettings.SingleAsync(x => x.Key == "system_timezone");
        setting.Value = "Invalid/Stored";
        await DbContextForArrange.SaveChangesAsync();
        Assert.Equal("UTC", (await zones.GetAsync()).Data!.SystemTimeZoneId);
        Assert.Equal("UTC", (await zones.GetConfiguredTimeZoneAsync()).Id);
    }

    [Fact]
    public async Task PendingOccurrence_ShouldKeepIdentityAcrossTimeZoneReset()
    {
        var definition = new TestDefinition("pending", false)
        {
            Occurrences =
            [
                new ScheduledJobOccurrence("maintenance", "{}", "a"),
                new ScheduledJobOccurrence("maintenance", "{broken", "b")
            ]
        };
        _definitions.Add(definition);
        var service = ResolveService<IScheduledJobService>();
        await service.EnqueueDueJobsAsync();
        _clock.Set(Start.AddHours(1));
        await Assert.ThrowsAsync<AggregateException>(() => service.EnqueueDueJobsAsync());
        _clock.Set(Start.AddHours(2));
        await ResolveService<ISystemTimeZoneService>()
            .UpdateAsync(new UpdateSystemTimeZoneRequest("Europe/London"));
        definition.Occurrences =
        [
            new ScheduledJobOccurrence("maintenance", "{}", "a"),
            new ScheduledJobOccurrence("maintenance", "{}", "b")
        ];

        var retry = await service.EnqueueDueJobsAsync();

        Assert.All(retry, item => Assert.Equal(Start.AddHours(1), item.DueAtUtc));
        Assert.Single(retry, item => item.Enqueued);
        Assert.Equal(2, await DbContextForAssert.Jobs.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task ListAndRunNow_ShouldReportScheduleJobsAndLeaveCheckpointUntouched()
    {
        var definition = new TestDefinition("manual", false) { Enabled = false };
        _definitions.Add(definition);
        var service = ResolveService<IScheduledJobService>();
        await service.EnqueueDueJobsAsync();
        var before = await DbContextForAssert.ScheduledJobSchedulerStates.AsNoTracking().SingleAsync();
        var first = await service.RunNowAsync("manual", ActorUserId);
        var second = await service.RunNowAsync("manual", ActorUserId);
        var list = await service.ListAsync();

        Assert.Equal(404, (await service.RunNowAsync("unknown")).StatusCode);
        Assert.Single(first.Data!.JobIds);
        Assert.Single(second.Data!.JobIds);
        Assert.NotEqual(first.Data.JobIds[0], second.Data.JobIds[0]);
        var jobs = await DbContextForAssert.Jobs.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        Assert.All(jobs, job =>
        {
            Assert.Equal(ActorUserId, job.UserId);
            Assert.StartsWith("adhoc:manual:", job.CorrelationId);
        });
        var item = Assert.Single(list.Data!);
        Assert.False(item.Enabled);
        Assert.Null(item.NextOccurrenceUtc);
        Assert.Equal(Start, item.LastEvaluatedAtUtc);
        Assert.NotNull(item.CurrentJob);
        Assert.Null(item.LatestStartedJob);
        var after = await DbContextForAssert.ScheduledJobSchedulerStates.AsNoTracking().SingleAsync();
        Assert.Equal(before.LastRunTimeUtc, after.LastRunTimeUtc);
        Assert.Equal(before.LastEvaluatedAtUtc, after.LastEvaluatedAtUtc);
    }

    [Fact]
    public async Task ManualFanOut_ShouldShareRunCorrelationButDistinguishIntentionalRuns()
    {
        _definitions.Add(new TestDefinition("fan", false)
        {
            Occurrences =
            [
                new ScheduledJobOccurrence("maintenance", "{}", "one"),
                new ScheduledJobOccurrence("maintenance", "{}", "two")
            ]
        });
        var service = ResolveService<IScheduledJobService>();
        var first = await service.RunNowAsync("fan");
        var second = await service.RunNowAsync("fan");

        Assert.Equal(2, first.Data!.EnqueuedCount);
        Assert.Equal(2, second.Data!.EnqueuedCount);
        var jobs = await DbContextForAssert.Jobs.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(jobs[0].CorrelationId, jobs[1].CorrelationId);
        Assert.Equal(jobs[2].CorrelationId, jobs[3].CorrelationId);
        Assert.NotEqual(jobs[0].CorrelationId, jobs[2].CorrelationId);
        Assert.Empty(await DbContextForAssert.ScheduledJobSchedulerStates.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task List_ShouldFindStartedJobByCorrelationPrefix()
    {
        _definitions.Add(new TestDefinition("history", false));
        DbContextForArrange.Jobs.AddRange(
            new EntityJob
            {
                Type = "maintenance", Status = JobStatus.Completed, RunAfterUtc = Start,
                StartedAtUtc = Start.AddMinutes(-2), CompletedAtUtc = Start.AddMinutes(-1),
                CorrelationId = "scheduled:history:20260926T110000Z"
            },
            new EntityJob
            {
                Type = "maintenance", Status = JobStatus.Running, RunAfterUtc = Start,
                StartedAtUtc = Start.AddMinutes(-1),
                CorrelationId = "adhoc:history:manual"
            },
            new EntityJob
            {
                Type = "maintenance", Status = JobStatus.Running, RunAfterUtc = Start,
                StartedAtUtc = Start,
                CorrelationId = "scheduled:history-other:20260926T120000Z"
            });
        await DbContextForArrange.SaveChangesAsync();

        var item = Assert.Single((await ResolveService<IScheduledJobService>().ListAsync()).Data!);

        Assert.NotNull(item.CurrentJob);
        Assert.Equal("running", item.CurrentJob.Status);
        Assert.Equal(item.CurrentJob.Id, item.LatestStartedJob!.Id);
    }

    [Fact]
    public async Task RunNowPartialFailure_ShouldReportSavedIds()
    {
        _definitions.Add(new TestDefinition("manual", false)
        {
            Occurrences =
            [
                new ScheduledJobOccurrence("maintenance", "{}", "a"),
                new ScheduledJobOccurrence("maintenance", "{broken", "b")
            ]
        });

        var result = await ResolveService<IScheduledJobService>().RunNowAsync("manual");

        Assert.False(result.Success);
        Assert.Equal(500, result.StatusCode);
        Assert.Single(result.Data!.JobIds);
        Assert.Contains("1 job(s) were confirmed saved", result.Message);
        Assert.Single(await DbContextForAssert.Jobs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task RegistrationAndFanOutValidation_ShouldRejectAmbiguousKeys()
    {
        _definitions.Add(new TestDefinition("same", false));
        _definitions.Add(new TestDefinition("SAME", false));
        Assert.Throws<InvalidOperationException>(() => ResolveService<IScheduledJobService>());
        _definitions.Clear();
        _definitions.Add(new TestDefinition("bad", true)
        {
            Occurrences =
            [
                new ScheduledJobOccurrence("maintenance", "{}", "same"),
                new ScheduledJobOccurrence("maintenance", "{}", "same")
            ]
        });
        await Assert.ThrowsAsync<AggregateException>(() =>
            ResolveService<IScheduledJobService>().EnqueueDueJobsAsync());
        Assert.Empty(await DbContextForAssert.Jobs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public void Registration_ShouldRejectDuplicateCheckpointAndUnsafeName()
    {
        _definitions.Add(new TestDefinition("first", false) { StateName = "shared" });
        _definitions.Add(new TestDefinition("second", false) { StateName = "shared" });
        Assert.Contains("Duplicate schedule checkpoint",
            Assert.Throws<InvalidOperationException>(() => ResolveService<IScheduledJobService>()).Message);
        _definitions.Clear();
        _definitions.Add(new TestDefinition("bad:name", false));
        Assert.Contains("Schedule name",
            Assert.Throws<InvalidOperationException>(() => ResolveService<IScheduledJobService>()).Message);
    }

    private sealed class TestDefinition(string name, bool initial) : IScheduledJobDefinition
    {
        public string Name => name;
        public string DisplayName => name;
        public string SchedulerStateName => StateName ?? $"{name}-state";
        public string? StateName { get; set; }
        public string Cron { get; set; } = "0 * * * *";
        public bool Enabled { get; set; } = true;
        public IReadOnlyList<ScheduledJobOccurrence> Occurrences { get; set; } =
            [new ScheduledJobOccurrence("maintenance")];
        public Func<Task>? BeforeConfiguration { get; set; }

        public async Task<ScheduledJobDefinitionConfiguration> GetConfigurationAsync(
            CancellationToken cancellationToken = default)
        {
            if (BeforeConfiguration is not null)
            {
                await BeforeConfiguration();
            }

            return new ScheduledJobDefinitionConfiguration(Enabled, Cron, initial);
        }

        public Task<IReadOnlyList<ScheduledJobOccurrence>> CreateOccurrencesAsync(
            DateTime dueAtUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult(Occurrences);
    }

    private sealed class TestClock(DateTime initial) : TimeProvider
    {
        private DateTimeOffset _now = new(initial);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Set(DateTime now) => _now = new DateTimeOffset(now);
    }

    private sealed class SaveFaults
    {
        public bool FinalCheckpoint { get; private set; }
        public bool TimeZone { get; private set; }
        public bool JobAfterCommit { get; private set; }
        public void FailNextFinalCheckpoint() => FinalCheckpoint = true;
        public void FailNextTimeZoneSave() => TimeZone = true;
        public void FailNextJobAfterCommit() => JobAfterCommit = true;
        public bool ShouldFailJobAfterCommit(BoardOilDbContext db)
        {
            if (!JobAfterCommit || !db.ChangeTracker.Entries<EntityJob>()
                    .Any(x => x.State == EntityState.Added))
            {
                return false;
            }

            JobAfterCommit = false;
            return true;
        }
        public bool ShouldFail(BoardOilDbContext db)
        {
            if (FinalCheckpoint && db.ChangeTracker.Entries<EntityScheduledJobSchedulerState>()
                    .Any(x => x.Entity.PendingDueAtUtc is null && x.Entity.LastEvaluatedAtUtc is not null))
            {
                FinalCheckpoint = false;
                return true;
            }

            if (TimeZone && db.ChangeTracker.Entries<EntityAppSetting>().Any())
            {
                TimeZone = false;
                return true;
            }

            return false;
        }
    }

    private sealed class InjectedSaveException : Exception;

    private sealed class FaultingScopeFactory(IDbContextScopeFactory inner, SaveFaults faults)
        : IDbContextScopeFactory
    {
        public IDbContextScope Create(DbContextScopeOption option = DbContextScopeOption.JoinExisting) =>
            new FaultingScope(inner.Create(option), faults);
        public IDbContextReadOnlyScope CreateReadOnly(DbContextScopeOption option = DbContextScopeOption.JoinExisting) =>
            inner.CreateReadOnly(option);
        public IDbContextScope CreateWithTransaction(IsolationLevel level) =>
            new FaultingScope(inner.CreateWithTransaction(level), faults);
        public IDbContextReadOnlyScope CreateReadOnlyWithTransaction(IsolationLevel level) =>
            inner.CreateReadOnlyWithTransaction(level);
        public IDisposable SuppressAmbientContext() => inner.SuppressAmbientContext();
    }

    private sealed class FaultingScope(IDbContextScope inner, SaveFaults faults) : IDbContextScope
    {
        public IDbContextCollection DbContexts => inner.DbContexts;
        public void Dispose() => inner.Dispose();
        public Task Transaction(Func<IDbContextTransactionScope, IDbContextTransaction, Task> executor) =>
            inner.Transaction(executor);
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var db = DbContexts.Get<BoardOilDbContext>();
            if (faults.ShouldFail(db))
            {
                throw new InjectedSaveException();
            }

            var afterCommit = faults.ShouldFailJobAfterCommit(db);
            var saved = await inner.SaveChangesAsync(cancellationToken);
            if (afterCommit)
            {
                throw new InjectedSaveException();
            }

            return saved;
        }
    }
}
