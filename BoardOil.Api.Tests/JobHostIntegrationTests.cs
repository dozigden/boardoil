using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Jobs;
using BoardOil.Api.Jobs;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.Jobs;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using BoardOil.Services.Jobs;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class JobHostIntegrationTests
{
    private static readonly DateTime FixedNow = new(2026, 9, 27, 4, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("20260912173931_AddBoardCardAttachmentThumbnailsSetting")]
    [InlineData("20260927150238_AddScheduledJobRunRequest")]
    public async Task Upgrade_ShouldResaveEachBoardAfterMigrationAndNotRepeatOnRestart(string previousMigration)
    {
        var directory = Directory.CreateTempSubdirectory("boardoil-resave-upgrade-");
        var path = Path.Combine(directory.FullName, "jobs.db");
        var options = new DbContextOptionsBuilder<BoardOilDbContext>()
            .UseSqlite($"Data Source={path}").UseOpenIddict().Options;
        try
        {
            await using (var arrange = new BoardOilDbContext(options))
            {
                await arrange.Database.MigrateAsync(previousMigration);
                await arrange.Database.ExecuteSqlRawAsync("""
                    INSERT INTO "Boards" ("Id", "Name", "Description", "CreatedAtUtc", "UpdatedAtUtc")
                    VALUES (1, 'First', '', '2026-09-01T00:00:00Z', '2026-09-01T00:00:00Z'),
                           (2, 'Second', '', '2026-09-01T00:00:00Z', '2026-09-01T00:00:00Z');
                    INSERT INTO "CardTypes" ("Id", "BoardId", "Name", "StyleName", "StylePropertiesJson", "IsSystem", "CreatedAtUtc", "UpdatedAtUtc")
                    VALUES (1, 1, 'Story', 'auto', '{{}}', 1, '2026-09-01T00:00:00Z', '2026-09-01T00:00:00Z'),
                           (2, 2, 'Story', 'auto', '{{}}', 1, '2026-09-01T00:00:00Z', '2026-09-01T00:00:00Z');
                    INSERT INTO "Columns" ("Id", "BoardId", "Title", "SortKey", "CreatedAtUtc", "UpdatedAtUtc")
                    VALUES (1, 1, 'Todo', 'U', '2026-09-01T00:00:00Z', '2026-09-01T00:00:00Z'),
                           (2, 2, 'Todo', 'U', '2026-09-01T00:00:00Z', '2026-09-01T00:00:00Z');
                    INSERT INTO "Cards" ("Id", "BoardId", "BoardCardId", "BoardColumnId", "CardTypeId", "Title", "Description", "SortKey", "CardCreatedUtc", "CardUpdatedUtc", "CreatedAtUtc", "UpdatedAtUtc")
                    VALUES (1, 1, 1, 1, 1, 'Completed item', '- [x] Done', 'U', '2026-09-01T00:00:00Z', '2026-09-01T00:00:00Z', '2026-09-01T00:00:00Z', '2026-09-01T00:00:00Z'),
                           (2, 2, 1, 2, 2, 'Open item', '- [ ] Open', 'U', '2026-09-01T00:00:00Z', '2026-09-01T00:00:00Z', '2026-09-01T00:00:00Z', '2026-09-01T00:00:00Z');
                    """);
            }

            var signals = new HostSignals();
            DateTime[] savedAt;
            await using (var host = CreateHostFactory(path, new BlockingHandler(), signals))
            {
                using var client = host.CreateClient();
                await signals.Idle.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await using var db = CreateDb(host.Services);
                var cards = await db.Cards.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync();
                Assert.Equal(2, cards.Length);
                Assert.Equal([1, 0], cards.Select(x => x.CompletedChecklistItemCount));
                Assert.All(cards, card =>
                {
                    Assert.Equal(1, card.TotalChecklistItemCount);
                    Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), card.CardUpdatedUtc);
                    Assert.Equal("U", card.SortKey);
                });
                Assert.Equal(["- [x] Done", "- [ ] Open"], cards.Select(x => x.Description));
                var jobs = await db.Jobs.AsNoTracking().Where(x => x.Type == ResaveBoardJobHandler.JobType).ToArrayAsync();
                Assert.Equal(2, jobs.Length);
                Assert.All(jobs, job => Assert.Equal(JobStatus.Completed, job.Status));
                var checkpoint = await db.ScheduledJobSchedulerStates.SingleAsync(x => x.Name == "resave-all-boards-checkpoint");
                Assert.False(checkpoint.RunRequested);
                Assert.Null(checkpoint.PendingDueAtUtc);
                savedAt = cards.Select(x => x.UpdatedAtUtc).ToArray();
            }

            var restartSignals = new HostSignals();
            await using (var restarted = CreateHostFactory(path, new BlockingHandler(), restartSignals))
            {
                using var client = restarted.CreateClient();
                await restartSignals.Idle.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await using var db = CreateDb(restarted.Services);
                Assert.Equal(2, await db.Jobs.CountAsync(x => x.Type == ResaveBoardJobHandler.JobType));
                Assert.Equal(savedAt, await db.Cards.OrderBy(x => x.Id).Select(x => x.UpdatedAtUtc).ToArrayAsync());
            }
        }
        finally
        {
            DeleteDisposableDatabase(directory, path);
        }
    }

    [Fact]
    public async Task RealHostMigratesFreshDatabaseBeforeRecoveryAndRegistersAllMaintenanceHandlers()
    {
        var directory = Directory.CreateTempSubdirectory("boardoil-job-host-");
        var path = Path.Combine(directory.FullName, "jobs.db");
        // The existing empty file bypasses the factory's migrated database template.
        File.WriteAllBytes(path, []);
        try
        {
            var signals = new HostSignals();
            await using (var factory = CreateHostFactory(path, new BlockingHandler(), signals))
            {
                using var client = factory.CreateClient();
                await signals.Recovery.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await signals.Schedule.Task.WaitAsync(TimeSpan.FromSeconds(5));
                using var scope = factory.Services.CreateScope();
                Assert.Equal(1, scope.ServiceProvider.GetServices<IHostedService>()
                    .Count(service => service is JobSchedulerService));
                var definitions = scope.ServiceProvider.GetServices<IScheduledJobDefinition>().ToArray();
                var handlers = scope.ServiceProvider.GetServices<IJobHandler>().ToArray();
                Assert.Equal(5, definitions.Length);
                foreach (var definition in definitions)
                {
                    var configuration = await definition.GetConfigurationAsync();
                    if (configuration.Kind == ScheduledJobKind.Once)
                    {
                        Assert.Null(configuration.DailyTime);
                    }
                    else
                    {
                        Assert.Equal(new TimeOnly(3, 0), configuration.DailyTime);
                    }
                    var occurrence = Assert.Single(await definition.CreateOccurrencesAsync(FixedNow));
                    Assert.Contains(handlers, handler => handler.Type == occurrence.JobType);
                }
                await using var db = CreateDb(factory.Services);
                Assert.Equal(5, await db.ScheduledJobSchedulerStates.AsNoTracking().CountAsync());
                Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
            }
        }
        finally
        {
            DeleteDisposableDatabase(directory, path);
        }
    }

    [Fact]
    public async Task RealHostRunsPendingJobAndRestartRecoversOnlyInterruptedWorkWithoutDuplicateCatchUp()
    {
        var directory = Directory.CreateTempSubdirectory("boardoil-job-restart-");
        var path = Path.Combine(directory.FullName, "jobs.db");
        try
        {
            int pendingId;
            int interruptedId;
            int terminalId;
            await using (var seedFactory = new BoardOilApiFactory(path))
            {
                using var seedClient = seedFactory.CreateClient();
                await using var db = CreateDb(seedFactory.Services);
                var pending = NewJob(JobStatus.Pending);
                var interrupted = NewJob(JobStatus.Running);
                interrupted.StartedAtUtc = FixedNow.AddDays(-2);
                var terminal = NewJob(JobStatus.Completed);
                terminal.StartedAtUtc = FixedNow.AddDays(-2);
                terminal.CompletedAtUtc = FixedNow.AddDays(-2);
                db.Jobs.AddRange(pending, interrupted, terminal);
                db.ScheduledJobSchedulerStates.Add(new EntityScheduledJobSchedulerState
                {
                    Name = "error-log-purge-checkpoint",
                    LastRunTimeUtc = FixedNow.AddDays(-3),
                    LastEvaluatedAtUtc = FixedNow.AddDays(-3)
                });
                await db.SaveChangesAsync();
                pendingId = pending.Id;
                interruptedId = interrupted.Id;
                terminalId = terminal.Id;
            }

            var handler = new BlockingHandler();
            var firstSignals = new HostSignals();
            await using (var firstHost = CreateHostFactory(path, handler, firstSignals))
            {
                using var client = firstHost.CreateClient();
                await firstSignals.Recovery.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await firstSignals.Schedule.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await handler.Started.WaitAsync(TimeSpan.FromSeconds(5));
                await using var db = CreateDb(firstHost.Services);
                Assert.Equal(JobStatus.Running,
                    (await db.Jobs.AsNoTracking().SingleAsync(job => job.Id == pendingId)).Status);
                Assert.Equal(JobStatus.Failed,
                    (await db.Jobs.AsNoTracking().SingleAsync(job => job.Id == interruptedId)).Status);
                Assert.Equal(1, await db.JobLogs.AsNoTracking().CountAsync(log =>
                    log.JobId == interruptedId && log.Message == "Job interrupted by application restart."));
                Assert.Equal(1, await db.Jobs.AsNoTracking().CountAsync(job =>
                    job.CorrelationId == ScheduledJobCorrelationIds.Create(
                        "error-log-purge", FixedNow.AddHours(-1))));
                handler.Release();
                await firstSignals.RunCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(JobStatus.Completed,
                    (await db.Jobs.AsNoTracking().SingleAsync(job => job.Id == pendingId)).Status);
            }

            var secondSignals = new HostSignals();
            await using (var secondHost = CreateHostFactory(path, new BlockingHandler(), secondSignals))
            {
                using var client = secondHost.CreateClient();
                await secondSignals.Recovery.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await secondSignals.Schedule.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await using var db = CreateDb(secondHost.Services);
                Assert.Equal(JobStatus.Completed,
                    (await db.Jobs.AsNoTracking().SingleAsync(job => job.Id == pendingId)).Status);
                Assert.Equal(JobStatus.Completed,
                    (await db.Jobs.AsNoTracking().SingleAsync(job => job.Id == terminalId)).Status);
                Assert.Equal(JobStatus.Failed,
                    (await db.Jobs.AsNoTracking().SingleAsync(job => job.Id == interruptedId)).Status);
                Assert.Equal(1, await db.JobLogs.AsNoTracking().CountAsync(log =>
                    log.JobId == interruptedId && log.Message == "Job interrupted by application restart."));
                Assert.Equal(1, await db.Jobs.AsNoTracking().CountAsync(job =>
                    job.CorrelationId == ScheduledJobCorrelationIds.Create(
                        "error-log-purge", FixedNow.AddHours(-1))));
            }
        }
        finally
        {
            DeleteDisposableDatabase(directory, path);
        }
    }

    [Fact]
    public async Task RealPendingJobExecutesWhileScheduleEvaluationFails()
    {
        var directory = Directory.CreateTempSubdirectory("boardoil-job-failed-schedule-");
        var path = Path.Combine(directory.FullName, "jobs.db");
        try
        {
            int jobId;
            await using (var seedFactory = new BoardOilApiFactory(path))
            {
                using var client = seedFactory.CreateClient();
                await using var db = CreateDb(seedFactory.Services);
                var pending = NewJob(JobStatus.Pending);
                db.Jobs.Add(pending);
                await db.SaveChangesAsync();
                jobId = pending.Id;
            }

            var handler = new BlockingHandler();
            handler.Release();
            var signals = new HostSignals();
            await using (var host = CreateHostFactory(path, handler, signals, failSchedule: true))
            {
                using var client = host.CreateClient();
                await signals.Schedule.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await signals.RunCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await using var db = CreateDb(host.Services);
                Assert.Equal(JobStatus.Completed,
                    (await db.Jobs.AsNoTracking().SingleAsync(job => job.Id == jobId)).Status);
            }
        }
        finally
        {
            DeleteDisposableDatabase(directory, path);
        }
    }

    private static EntityJob NewJob(JobStatus status) => new()
    {
        Type = BlockingHandler.JobType,
        Status = status,
        RunAfterUtc = FixedNow.AddDays(-3)
    };

    private static BoardOilApiFactory CreateHostFactory(
        string path, BlockingHandler handler, HostSignals signals, bool failSchedule = false) =>
        new(path, runJobs: true, configureTestServices: services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedClock());
            services.AddScoped<IJobHandler>(_ => handler);
            services.AddScoped<JobRunner>();
            services.RemoveAll<IJobRunner>();
            services.AddScoped<IJobRunner>(provider =>
                new SignallingRunner(provider.GetRequiredService<JobRunner>(), signals));
            services.AddScoped<ScheduledJobService>();
            services.RemoveAll<IScheduledJobService>();
            services.AddScoped<IScheduledJobService>(provider =>
                new SignallingSchedule(provider.GetRequiredService<ScheduledJobService>(), signals, failSchedule));
        });

    private static BoardOilDbContext CreateDb(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IDbContextFactory>()
            .CreateDbContext<BoardOilDbContext>();
    }

    private static void DeleteDisposableDatabase(DirectoryInfo directory, string path)
    {
        using var connection = new SqliteConnection($"Data Source={path}");
        SqliteConnection.ClearPool(connection);
        directory.Delete(recursive: true);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(FixedNow);
    }

    private sealed class HostSignals
    {
        public TaskCompletionSource Recovery { get; } = NewSignal();
        public TaskCompletionSource Schedule { get; } = NewSignal();
        public TaskCompletionSource RunCompleted { get; } = NewSignal();
        public TaskCompletionSource Idle { get; } = NewSignal();

        private static TaskCompletionSource NewSignal() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class SignallingRunner(IJobRunner inner, HostSignals signals) : IJobRunner
    {
        public async Task MarkRunningJobsFailedAsync(CancellationToken cancellationToken = default)
        {
            await inner.MarkRunningJobsFailedAsync(cancellationToken);
            signals.Recovery.TrySetResult();
        }

        public async Task<bool> RunNextDueAsync(CancellationToken cancellationToken = default)
        {
            var ran = await inner.RunNextDueAsync(cancellationToken);
            if (ran) signals.RunCompleted.TrySetResult();
            else if (signals.Schedule.Task.IsCompletedSuccessfully) signals.Idle.TrySetResult();
            return ran;
        }
    }

    private sealed class SignallingSchedule(
        IScheduledJobService inner, HostSignals signals, bool failSchedule) : IScheduledJobService
    {
        public async Task<IReadOnlyList<ScheduledJobEnqueueResult>> EnqueueDueJobsAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (failSchedule) throw new InvalidOperationException("Simulated schedule failure.");
                return await inner.EnqueueDueJobsAsync(cancellationToken);
            }
            finally
            {
                signals.Schedule.TrySetResult();
            }
        }

        public Task<ApiResult<IReadOnlyList<ScheduledJobDto>>> ListAsync(
            CancellationToken cancellationToken = default) => inner.ListAsync(cancellationToken);

        public Task<ApiResult<RunScheduledJobNowResultDto>> RunNowAsync(
            string scheduleName, int? userId = null, CancellationToken cancellationToken = default) =>
            inner.RunNowAsync(scheduleName, userId, cancellationToken);
    }

    private sealed class BlockingHandler : IJobHandler
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public const string JobType = "test-blocked-host-job";
        public string Type => JobType;
        public Task Started => _started.Task;
        public void Release() => _release.TrySetResult();

        public async Task<JobHandlerResult> HandleAsync(JobContext context, CancellationToken cancellationToken)
        {
            _started.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return JobHandlerResult.Succeeded();
        }
    }
}
