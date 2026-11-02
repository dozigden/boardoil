using System.Data.Common;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Jobs;
using BoardOil.Ef;
using BoardOil.Ef.Repositories;
using BoardOil.Ef.Scope;
using BoardOil.Services.Configuration;
using BoardOil.Services.Jobs;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SQLitePCL;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class ScheduledJobLockTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 12, 1, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("delete", false, false)]
    [InlineData("delete", true, false)]
    [InlineData("delete", true, true)]
    [InlineData("wal", false, false)]
    [InlineData("wal", true, false)]
    [InlineData("wal", true, true)]
    public async Task Evaluation_ShouldPersistCheckpointWithConcurrentReader(
        string mode, bool existingCheckpoint, bool due)
    {
        // Arrange
        using var database = await Database.CreateAsync(mode, existingCheckpoint, due);
        using var reader = database.OpenConnection();
        using var readTransaction = HoldRead(reader);
        // Hold the reader through write-statement finalisation. The old implicit
        // commit fails before reaching this boundary in DELETE mode.
        database.Committing.BeforeCommit = readTransaction.Dispose;

        // Act
        var result = await database.Scheduler.EnqueueDueJobsAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        using var verify = database.CreateDbContext<BoardOilDbContext>();
        var state = await verify.ScheduledJobSchedulerStates.SingleAsync();
        Assert.Equal(Now, state.LastRunTimeUtc);
        Assert.Equal(Now, state.LastEvaluatedAtUtc);
        Assert.Null(state.PendingDueAtUtc);
        Assert.Empty(await verify.Jobs.ToListAsync());
        using var query = reader.CreateCommand();
        query.CommandText = "PRAGMA journal_mode";
        Assert.Equal(mode, query.ExecuteScalar());
    }

    [Fact]
    public async Task Evaluation_ShouldFinishWaitingCommitAfterReaderReleases()
    {
        // Arrange
        using var database = await Database.CreateAsync("delete", true, false, timeoutSeconds: 5);
        using var reader = database.OpenConnection();
        using var readTransaction = HoldRead(reader);
        using var probe = database.OpenConnection();
        // Native stepping observes BUSY immediately; the managed provider would retry it.
        Assert.Equal(raw.SQLITE_OK, raw.sqlite3_prepare_v2(probe.Handle,
            "SELECT count(*) FROM ScheduledJobSchedulerStates", out var statement));
        using var probeStatement = statement;
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var evaluation = Task.Factory.StartNew(
            () => database.Scheduler.EnqueueDueJobsAsync(cancellationToken),
            cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        var commitBlocked = false;
        var completedWhileLocked = false;
        try
        {
            while (!evaluation.IsCompleted)
            {
                var result = raw.sqlite3_step(probeStatement);
                raw.sqlite3_reset(probeStatement);
                // BEGIN IMMEDIATE permits new readers. Only the committing writer's
                // pending/exclusive lock blocks this fresh read in DELETE mode.
                if (result == raw.SQLITE_BUSY)
                {
                    commitBlocked = true;
                    completedWhileLocked = evaluation.IsCompleted;
                    break;
                }

                Assert.Equal(raw.SQLITE_ROW, result);
                await Task.Delay(10, cancellationToken);
            }
        }
        finally
        {
            readTransaction.Dispose();
            await evaluation;
        }

        // Assert
        Assert.True(commitBlocked, "The reader must remain locked until COMMIT is blocked.");
        Assert.False(completedWhileLocked);
        Assert.Empty(await evaluation);
        using var verify = database.CreateDbContext<BoardOilDbContext>();
        var state = await verify.ScheduledJobSchedulerStates.SingleAsync();
        Assert.Equal(Now, state.LastRunTimeUtc);
        Assert.Equal(Now, state.LastEvaluatedAtUtc);
        Assert.Null(state.PendingDueAtUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Evaluation_WhenReaderOutlastsTimeout_ShouldLeaveCheckpointUnchanged(bool due)
    {
        // Arrange
        using var database = await Database.CreateAsync("delete", true, due);
        using var reader = database.OpenConnection();
        using var readTransaction = HoldRead(reader);

        // Act
        var exception = await Assert.ThrowsAsync<AggregateException>(() =>
            database.Scheduler.EnqueueDueJobsAsync(TestContext.Current.CancellationToken));
        readTransaction.Dispose();

        // Assert
        var evaluationFailure = Assert.Single(exception.InnerExceptions);
        var sqlite = Assert.IsType<SqliteException>(evaluationFailure.GetBaseException());
        Assert.Equal(5, sqlite.SqliteErrorCode);
        using var verify = database.CreateDbContext<BoardOilDbContext>();
        var state = await verify.ScheduledJobSchedulerStates.SingleAsync();
        Assert.Equal(Baseline(due), state.LastRunTimeUtc);
        Assert.Equal(Baseline(due), state.LastEvaluatedAtUtc);
        Assert.Null(state.PendingDueAtUtc);
        Assert.Empty(await verify.Jobs.ToListAsync());
    }

    private static DateTime Baseline(bool due) => due ? Now.AddDays(-1) : Now.AddMinutes(-1);

    private static SqliteTransaction HoldRead(SqliteConnection connection)
    {
        var transaction = connection.BeginTransaction(deferred: true);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT count(*) FROM ScheduledJobSchedulerStates";
        command.ExecuteScalar();
        return transaction;
    }

    private sealed class Database : IDbContextFactory, IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("boardoil-schedule-lock-");
        private readonly string _connectionString;
        public CommitObserver Committing { get; } = new();
        public ScheduledJobService Scheduler { get; }

        private Database(int timeoutSeconds)
        {
            _connectionString = $"Data Source={Path.Combine(_directory.FullName, "test.db")};Pooling=False;Default Timeout={timeoutSeconds}";
            var scopes = new DbContextScopeFactory(this);
            var locator = new AmbientDbContextLocator();
            var states = new ScheduledJobSchedulerStateRepository(locator);
            var gate = new SchedulingGate();
            var clock = new FixedClock();
            var zones = new SystemTimeZoneService(scopes, new AppSettingRepository(locator), states, gate, clock);
            Scheduler = new ScheduledJobService(scopes, new JobRepository(locator), states, null!,
                [new EmptyDefinition()], zones, new DailyOccurrenceCalculator(), gate, clock);
        }

        public static async Task<Database> CreateAsync(string mode, bool existingCheckpoint, bool due, int timeoutSeconds = 1)
        {
            var database = new Database(timeoutSeconds);
            try
            {
                using var db = database.CreateDbContext<BoardOilDbContext>();
                await db.Database.EnsureCreatedAsync();
                if (existingCheckpoint)
                {
                    db.ScheduledJobSchedulerStates.Add(new()
                    {
                        Name = "test", LastRunTimeUtc = Baseline(due), LastEvaluatedAtUtc = Baseline(due)
                    });
                    await db.SaveChangesAsync();
                }
                using var connection = database.OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = mode == "delete" ? "PRAGMA journal_mode=DELETE" : "PRAGMA journal_mode=WAL";
                Assert.Equal(mode, command.ExecuteScalar());
                return database;
            }
            catch
            {
                database.Dispose();
                throw;
            }
        }

        public TDbContext CreateDbContext<TDbContext>() where TDbContext : DbContext =>
            (TDbContext)(DbContext)new BoardOilDbContext(new DbContextOptionsBuilder<BoardOilDbContext>()
                .UseSqlite(_connectionString).UseOpenIddict().AddInterceptors(Committing).Options);

        public SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            return connection;
        }

        public void Dispose() => _directory.Delete(true);
    }

    private sealed class CommitObserver : DbTransactionInterceptor
    {
        public Action? BeforeCommit { get; set; }
        public override InterceptionResult TransactionCommitting(
            DbTransaction transaction, TransactionEventData eventData, InterceptionResult result)
        {
            var callback = BeforeCommit;
            BeforeCommit = null;
            callback?.Invoke();
            return result;
        }
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    private sealed class EmptyDefinition : IScheduledJobDefinition
    {
        public string Name => "test";
        public string DisplayName => Name;
        public string SchedulerStateName => Name;
        public Task<ScheduledJobDefinitionConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ScheduledJobDefinitionConfiguration(true, new TimeOnly(12, 0)));
        public Task<IReadOnlyList<ScheduledJobOccurrence>> CreateOccurrencesAsync(
            DateTime dueAtUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ScheduledJobOccurrence>>([]);
    }
}
