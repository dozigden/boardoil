using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Jobs;
using BoardOil.Ef;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class JobPersistenceTests : TestBaseDb
{
    private static readonly DateTime Now = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task JobsAndSchedulerState_ShouldRoundTripFieldsAndAllowSharedCorrelation()
    {
        var first = CreateJob("scheduled:daily:2026-09-26", JobStatus.Completed, Now.AddHours(-2));
        first.PayloadJson = "{\"a\":1}";
        first.ResultJson = "{\"ok\":true}";
        first.ErrorMessage = "diagnostic";
        first.StartedAtUtc = Now.AddHours(-1);
        first.CompletedAtUtc = Now;
        first.UserId = ActorUserId;
        var second = CreateJob(first.CorrelationId!, JobStatus.Pending, Now.AddHours(1));
        var state = new EntityScheduledJobSchedulerState
        {
            Name = "daily",
            LastRunTimeUtc = Now.AddDays(-1),
            LastEvaluatedAtUtc = Now
        };
        DbContextForArrange.Jobs.AddRange(first, second);
        DbContextForArrange.ScheduledJobSchedulerStates.Add(state);
        await DbContextForArrange.SaveChangesAsync();

        using var scope = ResolveService<IDbContextScopeFactory>().CreateReadOnly();
        var jobs = ResolveService<IJobRepository>();
        var states = ResolveService<IScheduledJobSchedulerStateRepository>();
        var reloaded = await jobs.GetForUpdateAsync(first.Id);
        var namedState = await states.GetByNameAsync("daily");

        Assert.NotNull(reloaded);
        Assert.Equal("maintenance", reloaded.Type);
        Assert.Equal(JobStatus.Completed, reloaded.Status);
        Assert.Equal(first.PayloadJson, reloaded.PayloadJson);
        Assert.Equal(first.ResultJson, reloaded.ResultJson);
        Assert.Equal(first.ErrorMessage, reloaded.ErrorMessage);
        Assert.Equal(first.StartedAtUtc, reloaded.StartedAtUtc);
        Assert.Equal(first.CompletedAtUtc, reloaded.CompletedAtUtc);
        Assert.Equal(first.UserId, reloaded.UserId);
        Assert.Equal(first.CorrelationId, reloaded.CorrelationId);
        Assert.Equal(DateTimeKind.Utc, reloaded.RunAfterUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, reloaded.CreatedAtUtc.Kind);
        Assert.NotEqual(default, reloaded.CreatedAtUtc);
        Assert.NotEqual(default, reloaded.UpdatedAtUtc);
        Assert.Equal("{}", (await jobs.GetForUpdateAsync(second.Id))!.PayloadJson);
        Assert.True(await jobs.ExistsByCorrelationIdAsync(first.CorrelationId!));
        Assert.NotNull(namedState);
        Assert.Equal(Now.AddDays(-1), namedState.LastRunTimeUtc);
        Assert.Equal(Now, namedState.LastEvaluatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, namedState.LastRunTimeUtc.Kind);
        Assert.NotEqual(default, namedState.CreatedAtUtc);
        Assert.Single(await states.ListAsync());
    }

    [Fact]
    public async Task JobQueries_ShouldOrderDueHistoryAndLogsWithoutLoadingLogsInHeader()
    {
        var early = CreateJob("scheduled:daily:1", JobStatus.Pending, Now.AddMinutes(-1));
        var tied = CreateJob("adhoc:daily:2", JobStatus.Pending, Now.AddMinutes(-1));
        var future = CreateJob("scheduled:daily:3", JobStatus.Pending, Now.AddMinutes(1));
        var running = CreateJob("adhoc:daily:4", JobStatus.Running, Now.AddMinutes(-2));
        running.StartedAtUtc = Now.AddMinutes(-3);
        var completed = CreateJob("other:5", JobStatus.Completed, Now.AddMinutes(-4));
        completed.StartedAtUtc = Now.AddMinutes(-10);
        early.Logs.Add(new EntityJobLog { Level = JobLogLevel.Error, Message = "later", LoggedAtUtc = Now });
        early.Logs.Add(new EntityJobLog { Level = JobLogLevel.Info, Message = "earlier", DataJson = "{\"step\":1}", LoggedAtUtc = Now.AddMinutes(-1) });
        early.Logs.Add(new EntityJobLog { Level = JobLogLevel.Warning, Message = "same time", LoggedAtUtc = Now });
        DbContextForArrange.Jobs.AddRange(early, tied, future, running, completed);
        await DbContextForArrange.SaveChangesAsync();

        using var scope = ResolveService<IDbContextScopeFactory>().CreateReadOnly();
        var jobs = ResolveService<IJobRepository>();
        var due = await jobs.FindNextDueAsync(Now);
        var headers = await jobs.ListAsync(1, 2);
        var header = await jobs.GetForUpdateAsync(early.Id);
        var details = await jobs.GetWithLogsAsync(early.Id);

        Assert.Equal(early.Id, due!.Id);
        Assert.Equal(5, await jobs.CountAsync());
        Assert.Equal(4, await jobs.CountActiveAsync());
        Assert.Equal([running.Id, future.Id], headers.Select(x => x.Id));
        Assert.Empty(header!.Logs);
        Assert.Equal(["earlier", "later", "same time"], details!.Logs.Select(x => x.Message));
        Assert.Equal("{\"step\":1}", details.Logs[0].DataJson);
        Assert.Equal([JobLogLevel.Info, JobLogLevel.Error, JobLogLevel.Warning], details.Logs.Select(x => x.Level));
        Assert.Equal(running.Id, (await jobs.ListRunningForUpdateAsync()).Single().Id);
        Assert.Equal(running.Id, (await jobs.GetLatestActiveByCorrelationPrefixesAsync("scheduled:", "adhoc:"))!.Id);
        Assert.Equal(running.Id, (await jobs.GetLatestStartedByCorrelationPrefixesAsync("scheduled:", "adhoc:"))!.Id);
    }

    [Fact]
    public async Task FindNextDue_ShouldExcludeFuturePendingJobs()
    {
        DbContextForArrange.Jobs.Add(CreateJob("scheduled:tomorrow", JobStatus.Pending, Now.AddDays(1)));
        await DbContextForArrange.SaveChangesAsync();

        using var scope = ResolveService<IDbContextScopeFactory>().CreateReadOnly();
        var due = await ResolveService<IJobRepository>().FindNextDueAsync(Now);

        Assert.Null(due);
    }

    [Fact]
    public async Task SchedulerNames_ShouldBeUnique()
    {
        DbContextForArrange.ScheduledJobSchedulerStates.Add(new EntityScheduledJobSchedulerState
        {
            Name = "daily", LastRunTimeUtc = Now
        });
        await DbContextForArrange.SaveChangesAsync();
        DbContextForArrange.ScheduledJobSchedulerStates.Add(new EntityScheduledJobSchedulerState
        {
            Name = "daily", LastRunTimeUtc = Now
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => DbContextForArrange.SaveChangesAsync());
    }

    [Fact]
    public async Task RetentionQueries_ShouldProtectActiveRecentAndReferencedJobsAndDeleteLogsInBatches()
    {
        var expired = CreateJob("expired", JobStatus.Completed, Now.AddDays(-10));
        expired.CompletedAtUtc = Now.AddDays(-9);
        expired.Logs.AddRange(
        [
            new EntityJobLog { Message = "one", LoggedAtUtc = Now.AddDays(-9) },
            new EntityJobLog { Message = "two", LoggedAtUtc = Now.AddDays(-9) },
            new EntityJobLog { Message = "three", LoggedAtUtc = Now.AddDays(-9) }
        ]);
        var referenced = CreateJob("referenced", JobStatus.Failed, Now.AddDays(-8));
        referenced.CompletedAtUtc = Now.AddDays(-8);
        var laterExpired = CreateJob("later expired", JobStatus.Cancelled, Now.AddDays(-7));
        laterExpired.CompletedAtUtc = Now.AddDays(-7);
        var recent = CreateJob("recent", JobStatus.Cancelled, Now.AddDays(-1));
        recent.CompletedAtUtc = Now.AddDays(-1);
        var active = CreateJob("active", JobStatus.Running, Now.AddDays(-20));
        active.CompletedAtUtc = Now.AddDays(-20);
        DbContextForArrange.Jobs.AddRange(expired, referenced, laterExpired, recent, active);
        DbContextForArrange.ErrorLogs.Add(new EntityErrorLog
        {
            Job = referenced,
            OccurredAtUtc = Now,
            Source = "test",
            Area = "jobs",
            ExceptionType = "TestException",
            Message = "retain"
        });
        await DbContextForArrange.SaveChangesAsync();

        using var scope = ResolveService<IDbContextScopeFactory>().Create();
        var jobs = ResolveService<IJobRepository>();
        var logs = ResolveService<IJobLogRepository>();
        var cutoff = Now.AddDays(-5);
        var candidate = await jobs.GetNextExpiredPurgeCandidateAsync(cutoff);
        var protectedDelete = await jobs.DeleteEmptyExpiredAsync(referenced.Id, cutoff);
        var prematureDelete = await jobs.DeleteEmptyExpiredAsync(expired.Id, cutoff);
        var firstBatch = await logs.DeleteBatchAsync(expired.Id, 2);
        var remainingLogIds = await DbContextForAssert.JobLogs
            .Where(log => log.JobId == expired.Id)
            .Select(log => log.Id)
            .ToListAsync();
        var secondBatch = await logs.DeleteBatchAsync(expired.Id, 2);
        var deletedHeader = await jobs.DeleteEmptyExpiredAsync(expired.Id, cutoff);

        Assert.Equal(expired.Id, candidate!.Id);
        Assert.Equal(0, protectedDelete);
        Assert.Equal(0, prematureDelete);
        Assert.Equal(2, firstBatch);
        Assert.Equal([expired.Logs[2].Id], remainingLogIds);
        Assert.Equal(1, secondBatch);
        Assert.Equal(1, deletedHeader);
        Assert.Equal(laterExpired.Id, (await jobs.GetNextExpiredPurgeCandidateAsync(cutoff))!.Id);
        Assert.Equal(4, await jobs.CountAsync());
        Assert.Equal(referenced.Id, (await DbContextForAssert.ErrorLogs.SingleAsync()).JobId);
    }

    [Fact]
    public async Task DeletingJob_ShouldCascadeLogsAndClearErrorLogReference()
    {
        var job = CreateJob("cascade", JobStatus.Failed, Now);
        job.Logs.Add(new EntityJobLog { Message = "diagnostic", LoggedAtUtc = Now });
        var error = new EntityErrorLog
        {
            Job = job, OccurredAtUtc = Now, Source = "test", Area = "jobs",
            ExceptionType = "TestException", Message = "referenced"
        };
        using (var setupScope = ResolveService<IDbContextScopeFactory>().Create())
        {
            setupScope.DbContexts.Get<BoardOilDbContext>().ErrorLogs.Add(error);
            await setupScope.SaveChangesAsync();
        }

        await using (var deleteDb = CreateDbContextForAct())
        {
            var header = await deleteDb.Jobs.SingleAsync(x => x.Id == job.Id);
            Assert.False(deleteDb.Entry(header).Collection(x => x.Logs).IsLoaded);
            Assert.Empty(deleteDb.ChangeTracker.Entries<EntityJobLog>());
            Assert.Empty(deleteDb.ChangeTracker.Entries<EntityErrorLog>());

            deleteDb.Jobs.Remove(header);
            await deleteDb.SaveChangesAsync();
        }

        Assert.Empty(await DbContextForAssert.Jobs.ToListAsync());
        Assert.Empty(await DbContextForAssert.JobLogs.ToListAsync());
        Assert.Null((await DbContextForAssert.ErrorLogs.SingleAsync()).JobId);
    }

    [Theory]
    [InlineData(JobStatus.Pending, -10)]
    [InlineData(JobStatus.Running, -10)]
    [InlineData(JobStatus.Completed, -5)]
    [InlineData(JobStatus.Failed, -5)]
    [InlineData(JobStatus.Cancelled, -5)]
    [InlineData(JobStatus.Completed, -1)]
    [InlineData(JobStatus.Failed, -1)]
    [InlineData(JobStatus.Cancelled, -1)]
    [InlineData(JobStatus.Completed, null)]
    [InlineData(JobStatus.Failed, null)]
    [InlineData(JobStatus.Cancelled, null)]
    public async Task RetentionQueries_ShouldProtectIneligibleJobs(JobStatus status, int? completedDaysAgo)
    {
        var cutoff = Now.AddDays(-5);
        var job = CreateJob("protected", status, Now.AddDays(-20));
        if (completedDaysAgo.HasValue)
        {
            job.CompletedAtUtc = Now.AddDays(completedDaysAgo.Value);
        }

        DbContextForArrange.Jobs.Add(job);
        await DbContextForArrange.SaveChangesAsync();

        using var scope = ResolveService<IDbContextScopeFactory>().Create();
        var jobs = ResolveService<IJobRepository>();
        var candidate = await jobs.GetNextExpiredPurgeCandidateAsync(cutoff);
        var deleted = await jobs.DeleteEmptyExpiredAsync(job.Id, cutoff);

        Assert.Null(candidate);
        Assert.Equal(0, deleted);
        Assert.Equal(job.Id, (await DbContextForAssert.Jobs.SingleAsync()).Id);
    }

    [Theory]
    [InlineData(JobStatus.Completed)]
    [InlineData(JobStatus.Failed)]
    [InlineData(JobStatus.Cancelled)]
    public async Task RetentionQueries_ShouldDeleteUnreferencedTerminalJobJustBeforeCutoff(JobStatus status)
    {
        var cutoff = Now.AddDays(-5);
        var job = CreateJob("expired", status, Now.AddDays(-20));
        job.CompletedAtUtc = cutoff.AddTicks(-1);
        DbContextForArrange.Jobs.Add(job);
        await DbContextForArrange.SaveChangesAsync();

        using var scope = ResolveService<IDbContextScopeFactory>().Create();
        var jobs = ResolveService<IJobRepository>();
        var candidate = await jobs.GetNextExpiredPurgeCandidateAsync(cutoff);
        var deleted = await jobs.DeleteEmptyExpiredAsync(job.Id, cutoff);

        Assert.Equal(job.Id, candidate!.Id);
        Assert.Equal(1, deleted);
        Assert.Empty(await DbContextForAssert.Jobs.ToListAsync());
    }

    private static EntityJob CreateJob(string correlationId, JobStatus status, DateTime runAfterUtc) =>
        new()
        {
            Type = "maintenance",
            CorrelationId = correlationId,
            Status = status,
            RunAfterUtc = runAfterUtc
        };
}
