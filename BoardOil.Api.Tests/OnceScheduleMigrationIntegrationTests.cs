using System.Globalization;
using BoardOil.Ef;
using BoardOil.Ef.Migrations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class OnceScheduleMigrationIntegrationTests
{
    private const string PreviousMigration = "20260926202234_AddPendingScheduleOccurrence";
    private const string TargetMigration = "20260927150238_AddScheduledJobRunRequest";
    private const string CheckpointName = "resave-all-boards-checkpoint";
    private static readonly DateTime Baseline = new(2026, 9, 26, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(null)]
    [InlineData(TargetMigration)]
    public async Task InitialActivation_ShouldOnlyArmRequestWithoutQueueingJobs(string? previousMigration)
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        await using (var arrange = new BoardOilDbContext(options))
        {
            if (previousMigration is not null)
            {
                await arrange.Database.MigrateAsync(previousMigration);
            }
        }

        await using (var migrate = new BoardOilDbContext(options))
        {
            await migrate.Database.MigrateAsync();
        }

        await using var assert = new BoardOilDbContext(options);
        var state = await assert.ScheduledJobSchedulerStates.SingleAsync();
        Assert.Equal(CheckpointName, state.Name);
        Assert.True(state.RunRequested);
        Assert.Null(state.PendingDueAtUtc);
        Assert.Null(state.LastEvaluatedAtUtc);
        Assert.Empty(await assert.Jobs.ToListAsync());
        Assert.False(assert.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Upgrade_ShouldDefaultRequestToFalseAndPreserveExistingCheckpoint()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        await using (var arrange = new BoardOilDbContext(options))
        {
            await arrange.Database.MigrateAsync(PreviousMigration);
            await arrange.Database.ExecuteSqlRawAsync("""
                INSERT INTO "ScheduledJobSchedulerStates"
                    ("Name", "LastRunTimeUtc", "LastEvaluatedAtUtc", "PendingDueAtUtc", "CreatedAtUtc", "UpdatedAtUtc")
                VALUES
                    ('existing-daily', '2026-09-26T10:00:00Z', '2026-09-26T11:00:00Z',
                     '2026-09-26T12:00:00Z', '2026-09-26T10:00:00Z', '2026-09-26T11:00:00Z');
                """);
        }

        await using (var migrate = new BoardOilDbContext(options))
        {
            await migrate.Database.MigrateAsync(TargetMigration);
            await migrate.Database.MigrateAsync(TargetMigration);
        }

        await using var assert = new BoardOilDbContext(options);
        var state = await ReadCheckpoints(assert).SingleAsync();
        Assert.False(state.RunRequested);
        Assert.Equal("existing-daily", state.Name);
        Assert.Equal(Baseline, state.LastRunTimeUtc);
        Assert.Equal(Baseline.AddHours(1), state.LastEvaluatedAtUtc);
        Assert.Equal(Baseline.AddHours(2), state.PendingDueAtUtc);
        Assert.Equal(Baseline, ParseTimestamp(state.CreatedAtUtc));
        Assert.Equal(Baseline.AddHours(1), state.UpdatedAtUtc);
        Assert.False(assert.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task FreshDatabase_ShouldDefaultNewCheckpointsToNoRequest()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        await using (var arrange = new BoardOilDbContext(options))
        {
            await arrange.Database.MigrateAsync(TargetMigration);
        }

        await using (var insert = new BoardOilDbContext(options))
        {
            // Omit the new column to verify the database default, not just the CLR default.
            await insert.Database.ExecuteSqlRawAsync("""
                INSERT INTO "ScheduledJobSchedulerStates"
                    ("Name", "LastRunTimeUtc", "CreatedAtUtc", "UpdatedAtUtc")
                VALUES ('unrequested', '2026-09-26T10:00:00Z', '2026-09-26T10:00:00Z', '2026-09-26T10:00:00Z');
                """);
        }

        await using var assert = new BoardOilDbContext(options);
        Assert.False((await ReadCheckpoints(assert).SingleAsync()).RunRequested);
        Assert.False(await HasJobsAsync(assert));
    }

    [Fact]
    public async Task RepeatedRequests_ShouldCreateOneDurableRequestWithoutQueueingJobs()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        await using (var arrange = new BoardOilDbContext(options))
        {
            await arrange.Database.MigrateAsync(TargetMigration);
        }

        await using (var request = new BoardOilDbContext(options))
        {
            await using var transaction = await request.Database.BeginTransactionAsync();
            await RequestAsync(request, CheckpointName);
            await RequestAsync(request, CheckpointName);
            await transaction.CommitAsync();
        }

        await using var assert = new BoardOilDbContext(options);
        var state = await ReadCheckpoints(assert).SingleAsync();
        Assert.Equal(CheckpointName, state.Name);
        Assert.True(state.RunRequested);
        Assert.Null(state.PendingDueAtUtc);
        Assert.Null(state.LastEvaluatedAtUtc);
        var createdAtUtc = ParseTimestamp(state.CreatedAtUtc);
        Assert.NotEqual(default, createdAtUtc);
        Assert.Equal(DateTimeKind.Utc, createdAtUtc.Kind);
        Assert.Equal(createdAtUtc, state.LastRunTimeUtc);
        Assert.True(state.UpdatedAtUtc >= createdAtUtc);
        Assert.False(await HasJobsAsync(assert));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rearm_ShouldClearOldOccurrenceAndPreserveOtherCheckpointFields(bool alreadyRequested)
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        var previousUpdatedAtUtc = Baseline.AddHours(1);
        await using (var arrange = new BoardOilDbContext(options))
        {
            await arrange.Database.MigrateAsync(TargetMigration);
            await InsertCheckpointAsync(arrange, CheckpointName, alreadyRequested, previousUpdatedAtUtc);
            await InsertCheckpointAsync(arrange, "unrelated-daily", false, previousUpdatedAtUtc);
        }

        await using (var request = new BoardOilDbContext(options))
        {
            await RequestAsync(request, CheckpointName);
        }

        await using var assert = new BoardOilDbContext(options);
        var stateAfter = await ReadCheckpoints(assert).SingleAsync(x => x.Name == CheckpointName);
        Assert.True(stateAfter.RunRequested);
        Assert.Null(stateAfter.PendingDueAtUtc);
        Assert.Equal(Baseline, stateAfter.LastRunTimeUtc);
        Assert.Equal(Baseline.AddHours(1), stateAfter.LastEvaluatedAtUtc);
        Assert.Equal(Baseline, ParseTimestamp(stateAfter.CreatedAtUtc));
        Assert.True(stateAfter.UpdatedAtUtc >= previousUpdatedAtUtc);
        var unrelated = await ReadCheckpoints(assert).SingleAsync(x => x.Name == "unrelated-daily");
        Assert.False(unrelated.RunRequested);
        Assert.Equal(Baseline.AddHours(2), unrelated.PendingDueAtUtc);
        Assert.Equal(previousUpdatedAtUtc, unrelated.UpdatedAtUtc);
    }

    [Fact]
    public async Task ClearingRequest_ShouldPersistCompletionAcrossContexts()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        await using (var arrange = new BoardOilDbContext(options))
        {
            await arrange.Database.MigrateAsync(TargetMigration);
            await InsertCheckpointAsync(arrange, CheckpointName, true, Baseline);
        }

        await using (var complete = new BoardOilDbContext(options))
        {
            var updatedAtUtc = DateTime.UtcNow;
            var affected = await complete.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE ScheduledJobSchedulerStates
                SET RunRequested = 0, PendingDueAtUtc = NULL, UpdatedAtUtc = {updatedAtUtc}
                WHERE Name = {CheckpointName}
                """);
            Assert.Equal(1, affected);
        }

        await using var assert = new BoardOilDbContext(options);
        var completed = await ReadCheckpoints(assert).SingleAsync();
        Assert.False(completed.RunRequested);
        Assert.Null(completed.PendingDueAtUtc);
    }

    [Fact]
    public async Task RolledBackMigration_ShouldNotLeaveInsertedOrRearmedRequests()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        await using (var arrange = new BoardOilDbContext(options))
        {
            await arrange.Database.MigrateAsync(TargetMigration);
            await InsertCheckpointAsync(arrange, CheckpointName, false, Baseline);
        }

        await using (var request = new BoardOilDbContext(options))
        {
            await using var transaction = await request.Database.BeginTransactionAsync();
            await RequestAsync(request, CheckpointName);
            await RequestAsync(request, "another-once-checkpoint");
            await transaction.RollbackAsync();
        }

        await using var assert = new BoardOilDbContext(options);
        var state = await ReadCheckpoints(assert).SingleAsync();
        Assert.Equal(CheckpointName, state.Name);
        Assert.False(state.RunRequested);
        Assert.Equal(Baseline.AddHours(2), state.PendingDueAtUtc);
    }

    private sealed record Checkpoint(
        string Name, bool RunRequested, DateTime LastRunTimeUtc, DateTime? LastEvaluatedAtUtc,
        DateTime? PendingDueAtUtc, string CreatedAtUtc, DateTime UpdatedAtUtc);

    private static IQueryable<Checkpoint> ReadCheckpoints(BoardOilDbContext db) => db.Database.SqlQueryRaw<Checkpoint>("""
        SELECT Name, RunRequested, LastRunTimeUtc, LastEvaluatedAtUtc, PendingDueAtUtc, CreatedAtUtc, UpdatedAtUtc
        FROM ScheduledJobSchedulerStates
        """);

    private static Task<bool> HasJobsAsync(BoardOilDbContext db) =>
        db.Database.SqlQueryRaw<int>("SELECT Id AS Value FROM Jobs").AnyAsync();

    private static DateTime ParseTimestamp(string timestamp) =>
        DateTime.Parse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static Task<int> InsertCheckpointAsync(BoardOilDbContext db, string name, bool requested, DateTime updatedAtUtc) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO ScheduledJobSchedulerStates
                (Name, RunRequested, LastRunTimeUtc, LastEvaluatedAtUtc, PendingDueAtUtc, CreatedAtUtc, UpdatedAtUtc)
            VALUES ({name}, {requested}, {Baseline}, {Baseline.AddHours(1)}, {Baseline.AddHours(2)}, {Baseline}, {updatedAtUtc});
            """);

    private static async Task RequestAsync(BoardOilDbContext db, string name)
    {
        var builder = new MigrationBuilder(db.Database.ProviderName!);
        builder.RequestOnceSchedule(name);
        var commands = db.GetService<IMigrationsSqlGenerator>().Generate(builder.Operations);
        foreach (var command in commands)
        {
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        }
    }

    private static DbContextOptions<BoardOilDbContext> CreateOptions(SqliteConnection connection) =>
        new DbContextOptionsBuilder<BoardOilDbContext>().UseSqlite(connection).Options;

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }
}
