using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.ErrorLogs;
using BoardOil.Abstractions.Jobs;
using BoardOil.Abstractions.OAuth;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.ErrorLogs;
using BoardOil.Contracts.Jobs;
using BoardOil.Contracts.OAuth;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Jobs;
using BoardOil.Services.Jobs;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class MaintenanceJobTests : TestBaseDb
{
    private static readonly DateTime Now = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddLogging();
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(new FixedClock(Now));
    }

    [Fact]
    public async Task Definitions_ShouldCreateFourInitialJobsWithStableNamesAndCadence()
    {
        var definitions = ResolveService<IEnumerable<IScheduledJobDefinition>>().ToArray();
        var expected = new[]
        {
            ("oauth-client-registration-purge", MaintenanceJobTypes.OAuthClientRegistrationPurge, new TimeOnly(3, 0)),
            ("error-log-purge", MaintenanceJobTypes.ErrorLogPurge, new TimeOnly(3, 0)),
            ("oauth-token-audit-purge", MaintenanceJobTypes.OAuthTokenAuditPurge, new TimeOnly(3, 0)),
            ("history-purge", MaintenanceJobTypes.HistoryPurge, new TimeOnly(3, 0))
        };

        Assert.Equal(expected.Length + 1, definitions.Length);
        Assert.Contains(definitions, item => item.Name == ResaveAllBoardsScheduledJobDefinition.ScheduleName);
        foreach (var (name, type, dailyTime) in expected)
        {
            var definition = Assert.Single(definitions, item => item.Name == name);
            var configuration = await definition.GetConfigurationAsync();
            var occurrence = Assert.Single(await definition.CreateOccurrencesAsync(Now));
            Assert.True(configuration.Enabled);
            Assert.True(configuration.RunOnInitialisation);
            Assert.Equal(dailyTime, configuration.DailyTime);
            Assert.Equal($"{name}-checkpoint", definition.SchedulerStateName);
            Assert.Equal(type, occurrence.JobType);
            Assert.Equal("{}", occurrence.PayloadJson);
            Assert.Null(occurrence.TargetKey);
            Assert.Equal($"scheduled:{name}:20260926T120000Z",
                ScheduledJobCorrelationIds.Create(name, Now, occurrence.TargetKey));
        }

        var first = await ResolveService<IScheduledJobService>().EnqueueDueJobsAsync();
        var second = await ResolveService<IScheduledJobService>().EnqueueDueJobsAsync();
        Assert.Equal(4, first.Count);
        Assert.Empty(second);
        Assert.Equal(expected.Select(item => item.Item2).Order(),
            (await DbContextForAssert.Jobs.AsNoTracking().ToListAsync()).Select(job => job.Type).Order());
    }

    [Fact]
    public async Task ServicePurgeHandlers_ShouldReturnAuthoritativeCutoffsAndCounts()
    {
        var errorCutoff = Now.AddDays(-ErrorLogRetention.RetentionDays);
        var auditCutoff = Now.AddDays(-OAuthTokenAuditRetention.RetentionDays);
        DbContextForArrange.ErrorLogs.AddRange(
            Error("old", errorCutoff.AddTicks(-1)), Error("equal", errorCutoff));
        DbContextForArrange.OAuthTokenAudits.AddRange(
            Audit("old", auditCutoff.AddTicks(-1)), Audit("equal", auditCutoff));
        await DbContextForArrange.SaveChangesAsync();
        var handlers = ResolveService<IEnumerable<IJobHandler>>().ToArray();

        var payloadWithIgnoredOverride = "{\"retentionDays\":1,\"cutoffUtc\":\"2000-01-01T00:00:00Z\"}";
        var error = await Assert.Single(handlers, x => x.Type == MaintenanceJobTypes.ErrorLogPurge)
            .HandleAsync(new JobContext(1, MaintenanceJobTypes.ErrorLogPurge, payloadWithIgnoredOverride), TestContext.Current.CancellationToken);
        var audit = await Assert.Single(handlers, x => x.Type == MaintenanceJobTypes.OAuthTokenAuditPurge)
            .HandleAsync(new JobContext(2, MaintenanceJobTypes.OAuthTokenAuditPurge, payloadWithIgnoredOverride), TestContext.Current.CancellationToken);

        Assert.True(error.Success);
        Assert.True(audit.Success);
        using var errorJson = JsonDocument.Parse(error.ResultJson);
        using var auditJson = JsonDocument.Parse(audit.ResultJson);
        Assert.Equal(ErrorLogRetention.RetentionDays, errorJson.RootElement.GetProperty("retentionDays").GetInt32());
        Assert.Equal(errorCutoff, errorJson.RootElement.GetProperty("cutoffUtc").GetDateTime());
        Assert.Equal(1, errorJson.RootElement.GetProperty("deletedCount").GetInt32());
        Assert.Equal(OAuthTokenAuditRetention.RetentionDays, auditJson.RootElement.GetProperty("retentionDays").GetInt32());
        Assert.Equal(auditCutoff, auditJson.RootElement.GetProperty("cutoffUtc").GetDateTime());
        Assert.Equal(1, auditJson.RootElement.GetProperty("deletedCount").GetInt32());
        Assert.Equal("equal", (await DbContextForAssert.ErrorLogs.SingleAsync()).Message);
        Assert.Equal("equal", (await DbContextForAssert.OAuthTokenAudits.SingleAsync()).ErrorCode);
    }

    [Fact]
    public async Task ServicePurgeHandlers_ShouldConvertFailuresAndPropagateCancellation()
    {
        var errorHandler = new ErrorLogPurgeJobHandler(new ErrorLogPurgeStub(token =>
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult<ApiResult<ErrorLogPurgeResultDto>>(ApiErrors.InternalError("error purge failed"));
        }));
        var auditHandler = new OAuthTokenAuditPurgeJobHandler(new TokenAuditPurgeStub(token =>
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult<ApiResult<OAuthTokenAuditPurgeResultDto>>(ApiErrors.InternalError("audit purge failed"));
        }));
        var errorContext = new JobContext(1, MaintenanceJobTypes.ErrorLogPurge, "{}");
        var auditContext = new JobContext(2, MaintenanceJobTypes.OAuthTokenAuditPurge, "{}");

        var error = await errorHandler.HandleAsync(errorContext, TestContext.Current.CancellationToken);
        var audit = await auditHandler.HandleAsync(auditContext, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.False(error.Success);
        Assert.Equal("error purge failed", error.ErrorMessage);
        Assert.False(audit.Success);
        Assert.Equal("audit purge failed", audit.ErrorMessage);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            errorHandler.HandleAsync(errorContext, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            auditHandler.HandleAsync(auditContext, cancellation.Token));

        var throwingErrorHandler = new ErrorLogPurgeJobHandler(new ErrorLogPurgeStub(
            _ => throw new InvalidOperationException("storage unavailable")));
        var throwingAuditHandler = new OAuthTokenAuditPurgeJobHandler(new TokenAuditPurgeStub(
            _ => throw new InvalidOperationException("storage unavailable")));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            throwingErrorHandler.HandleAsync(errorContext, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            throwingAuditHandler.HandleAsync(auditContext, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HistoryPurge_ShouldPreserveActiveRecentReferencedAndBoardData()
    {
        var cutoff = Now.AddDays(-HistoryPurgeJobHandler.RetentionDays);
        var oldCompleted = Job(JobStatus.Completed, cutoff.AddTicks(-1));
        oldCompleted.Logs.AddRange(Enumerable.Range(0, 1_002).Select(index =>
            new EntityJobLog { Message = $"detail {index}", LoggedAtUtc = cutoff }));
        var oldFailed = Job(JobStatus.Failed, cutoff.AddDays(-1));
        var oldCancelled = Job(JobStatus.Cancelled, cutoff.AddDays(-2));
        var referenced = Job(JobStatus.Failed, cutoff.AddDays(-3));
        referenced.Logs.Add(new EntityJobLog { Message = "diagnostic", LoggedAtUtc = cutoff });
        var exactCutoff = Job(JobStatus.Completed, cutoff);
        var recent = Job(JobStatus.Cancelled, cutoff.AddTicks(1));
        var pending = Job(JobStatus.Pending, cutoff.AddDays(-20));
        var running = Job(JobStatus.Running, cutoff.AddDays(-20));
        CreateBoard().AddColumn("Todo").AddCard("Keep card").Build();
        DbContextForArrange.Jobs.AddRange(oldCompleted, oldFailed, oldCancelled, referenced,
            exactCutoff, recent, pending, running);
        DbContextForArrange.ErrorLogs.Add(Error("retain", Now, referenced));
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<IEnumerable<IJobHandler>>().Single(x => x.Type == MaintenanceJobTypes.HistoryPurge)
            .HandleAsync(new JobContext(999, MaintenanceJobTypes.HistoryPurge, "{}"), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        using var json = JsonDocument.Parse(result.ResultJson);
        Assert.Equal(14, json.RootElement.GetProperty("retentionDays").GetInt32());
        Assert.Equal(cutoff, json.RootElement.GetProperty("cutoffUtc").GetDateTime());
        Assert.Equal(3, json.RootElement.GetProperty("deletedJobCount").GetInt32());
        Assert.Equal(1_002, json.RootElement.GetProperty("deletedLogCount").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("batchCount").GetInt32());
        Assert.Equal(1_000, json.RootElement.GetProperty("batchSize").GetInt32());
        var remainingIds = await DbContextForAssert.Jobs.AsNoTracking().Select(job => job.Id).ToArrayAsync();
        Assert.Equal(new[] { referenced.Id, exactCutoff.Id, recent.Id, pending.Id, running.Id }.Order(), remainingIds.Order());
        Assert.Equal(referenced.Id, (await DbContextForAssert.ErrorLogs.SingleAsync()).JobId);
        Assert.Single(await DbContextForAssert.JobLogs.ToListAsync());
        Assert.Equal("Keep card", (await DbContextForAssert.Cards.SingleAsync()).Title);
    }

    [Fact]
    public async Task HistoryPurge_ShouldResumeAfterCancellationBetweenCommittedBatches()
    {
        var expired = Job(JobStatus.Completed, Now.AddDays(-15));
        expired.Logs.AddRange(Enumerable.Range(0, 1_002).Select(index =>
            new EntityJobLog { Message = $"detail {index}", LoggedAtUtc = Now.AddDays(-15) }));
        DbContextForArrange.Jobs.Add(expired);
        await DbContextForArrange.SaveChangesAsync();
        using var cancellation = new CancellationTokenSource();
        var handler = new HistoryPurgeJobHandler(
            ResolveService<IDbContextScopeFactory>(), ResolveService<IJobRepository>(),
            new CancelAfterFirstBatch(ResolveService<IJobLogRepository>(), cancellation),
            new FixedClock(Now), NullLogger<HistoryPurgeJobHandler>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.HandleAsync(
            new JobContext(999, MaintenanceJobTypes.HistoryPurge, "{}"), cancellation.Token));
        Assert.Equal(2, await DbContextForAssert.JobLogs.CountAsync());
        Assert.Equal(expired.Id, (await DbContextForAssert.Jobs.SingleAsync()).Id);

        var rerun = await ResolveService<IEnumerable<IJobHandler>>().Single(x => x.Type == MaintenanceJobTypes.HistoryPurge)
            .HandleAsync(new JobContext(999, MaintenanceJobTypes.HistoryPurge, "{}"), TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(rerun.ResultJson);
        Assert.Equal(1, json.RootElement.GetProperty("deletedJobCount").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("deletedLogCount").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("batchCount").GetInt32());
        Assert.Empty(await DbContextForAssert.Jobs.ToListAsync());
        Assert.Empty(await DbContextForAssert.JobLogs.ToListAsync());
    }

    [Fact]
    public async Task HistoryPurge_ShouldDeleteJobOnceReferencingErrorExpires()
    {
        var job = Job(JobStatus.Failed, Now.AddDays(-100));
        DbContextForArrange.Jobs.Add(job);
        DbContextForArrange.ErrorLogs.Add(Error(
            "expired reference", Now.AddDays(-ErrorLogRetention.RetentionDays).AddTicks(-1), job));
        await DbContextForArrange.SaveChangesAsync();
        var historyHandler = ResolveService<IEnumerable<IJobHandler>>()
            .Single(x => x.Type == MaintenanceJobTypes.HistoryPurge);

        var protectedRun = await historyHandler.HandleAsync(
            new JobContext(999, MaintenanceJobTypes.HistoryPurge, "{}"), TestContext.Current.CancellationToken);
        var errorPurge = await ResolveService<IErrorLogService>().PurgeExpiredAsync(TestContext.Current.CancellationToken);
        var eligibleRun = await historyHandler.HandleAsync(
            new JobContext(999, MaintenanceJobTypes.HistoryPurge, "{}"), TestContext.Current.CancellationToken);

        Assert.True(protectedRun.Success);
        Assert.True(errorPurge.Success);
        Assert.Equal(1, errorPurge.Data!.DeletedCount);
        Assert.True(eligibleRun.Success);
        Assert.Equal(1, JsonDocument.Parse(eligibleRun.ResultJson).RootElement
            .GetProperty("deletedJobCount").GetInt32());
        Assert.Empty(await DbContextForAssert.Jobs.ToListAsync());
    }

    private static EntityJob Job(JobStatus status, DateTime completedAtUtc) => new()
    {
        Type = "test", Status = status, RunAfterUtc = completedAtUtc,
        CompletedAtUtc = completedAtUtc
    };

    private static EntityErrorLog Error(string message, DateTime occurredAtUtc, EntityJob? job = null) => new()
    {
        OccurredAtUtc = occurredAtUtc, Source = "test", Area = "jobs",
        ExceptionType = "TestException", Message = message, Job = job
    };

    private static EntityOAuthTokenAudit Audit(string errorCode, DateTime occurredAtUtc) => new()
    {
        OccurredAtUtc = occurredAtUtc, Outcome = OAuthTokenAuditOutcomes.Rejected,
        GrantType = "refresh_token", ErrorCode = errorCode, OAuthClientId = "client"
    };

    private sealed class FixedClock(DateTime nowUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(nowUtc);
    }

    private sealed class CancelAfterFirstBatch(IJobLogRepository inner, CancellationTokenSource cancellation)
        : IJobLogRepository
    {
        public void Add(EntityJobLog log) => inner.Add(log);

        public async Task<int> DeleteBatchAsync(int jobId, int batchSize, CancellationToken cancellationToken = default)
        {
            var deleted = await inner.DeleteBatchAsync(jobId, batchSize, cancellationToken);
            if (deleted > 0)
            {
                cancellation.Cancel();
            }

            return deleted;
        }
    }

    private sealed class ErrorLogPurgeStub(
        Func<CancellationToken, Task<ApiResult<ErrorLogPurgeResultDto>>> purge) : IErrorLogService
    {
        public Task<ApiResult<ErrorLogPurgeResultDto>> PurgeExpiredAsync(CancellationToken cancellationToken = default) =>
            purge(cancellationToken);
        public Task<ApiResult<ErrorLogListDto>> ListAsync(int? offset, int? limit) => throw new NotSupportedException();
        public Task<ApiResult<ErrorLogDetailsDto>> GetAsync(int id) => throw new NotSupportedException();
        public Task<ApiResult<ErrorLogDto>> ReportClientErrorAsync(
            ClientErrorReportRequest request, int actorUserId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<int?> LogExceptionAsync(
            Exception exception, ErrorLogContext context, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TokenAuditPurgeStub(
        Func<CancellationToken, Task<ApiResult<OAuthTokenAuditPurgeResultDto>>> purge) : IOAuthTokenAuditService
    {
        public Task<ApiResult<OAuthTokenAuditPurgeResultDto>> PurgeExpiredAsync(CancellationToken cancellationToken = default) =>
            purge(cancellationToken);
        public Task<ApiResult<OAuthTokenAuditListDto>> ListAsync(
            int? offset, int? limit, DateTime? fromUtc, DateTime? toUtc,
            string? outcome, string? grantType, int? connectionId, string? clientId,
            string? authorizationId, string? tokenFingerprint) => throw new NotSupportedException();
        public Task RecordAsync(OAuthTokenAuditInput input) => throw new NotSupportedException();
    }
}
