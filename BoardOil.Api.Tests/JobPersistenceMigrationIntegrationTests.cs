using BoardOil.Ef;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class JobPersistenceMigrationIntegrationTests
{
    private const string TargetMigration = "20260926163515_AddJobPersistence";

    [Fact]
    public async Task AddJobPersistence_ShouldPreserveExistingBoardCardAndErrorLogOnRepeatedMigration()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"boardoil-job-migration-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<BoardOilDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        try
        {
            await using (var db = new BoardOilDbContext(options))
            {
                var migrations = db.Database.GetMigrations().ToList();
                var targetIndex = migrations.IndexOf(TargetMigration);
                Assert.True(targetIndex > 0, "The job migration must have an existing predecessor.");
                await db.Database.MigrateAsync(migrations[targetIndex - 1]);
            }

            await using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO "Boards" ("Id", "Name", "Description", "CreatedAtUtc", "UpdatedAtUtc")
                    VALUES (1, 'Existing board', '', '2026-09-01T12:00:00Z', '2026-09-01T12:00:00Z');

                    INSERT INTO "CardTypes" ("Id", "BoardId", "Name", "StyleName", "StylePropertiesJson", "IsSystem", "CreatedAtUtc", "UpdatedAtUtc")
                    VALUES (1, 1, 'Story', 'auto', '{}', 1, '2026-09-01T12:00:00Z', '2026-09-01T12:00:00Z');

                    INSERT INTO "Columns" ("Id", "BoardId", "Title", "SortKey", "CreatedAtUtc", "UpdatedAtUtc")
                    VALUES (1, 1, 'Todo', 'A', '2026-09-01T12:00:00Z', '2026-09-01T12:00:00Z');

                    INSERT INTO "Cards" ("Id", "BoardId", "BoardCardId", "BoardColumnId", "CardTypeId", "Title", "Description", "SortKey", "CardCreatedUtc", "CardUpdatedUtc", "CreatedAtUtc", "UpdatedAtUtc")
                    VALUES (1, 1, 1, 1, 1, 'Existing card', '', 'A', '2026-09-01T12:00:00Z', '2026-09-01T12:00:00Z', '2026-09-01T12:00:00Z', '2026-09-01T12:00:00Z');

                    INSERT INTO "ErrorLogs" ("Id", "OccurredAtUtc", "Source", "Area", "ExceptionType", "Message", "CreatedAtUtc", "UpdatedAtUtc")
                    VALUES (1, '2026-09-01T12:00:00Z', 'api', 'cards', 'TestException', 'Existing error', '2026-09-01T12:00:00Z', '2026-09-01T12:00:00Z');
                    """;
                await command.ExecuteNonQueryAsync();
            }

            await using (var db = new BoardOilDbContext(options))
            {
                await db.Database.MigrateAsync(TargetMigration);
                await db.Database.MigrateAsync(TargetMigration);
            }

            // Inspect the historical schema directly: the current EF model may gain columns later.
            await using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT
                        (SELECT "Name" FROM "Boards" WHERE "Id" = 1),
                        (SELECT "Title" FROM "Cards" WHERE "Id" = 1),
                        (SELECT "Message" FROM "ErrorLogs" WHERE "Id" = 1),
                        (SELECT "JobId" FROM "ErrorLogs" WHERE "Id" = 1),
                        (SELECT COUNT(*) FROM "Jobs"),
                        (SELECT COUNT(*) FROM "JobLogs"),
                        (SELECT COUNT(*) FROM "ScheduledJobSchedulerStates");
                    """;
                await using var reader = await command.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal("Existing board", reader.GetString(0));
                Assert.Equal("Existing card", reader.GetString(1));
                Assert.Equal("Existing error", reader.GetString(2));
                Assert.True(reader.IsDBNull(3));
                Assert.Equal(0, reader.GetInt64(4));
                Assert.Equal(0, reader.GetInt64(5));
                Assert.Equal(0, reader.GetInt64(6));
            }
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task AddJobPersistence_ShouldApplyCompleteMigrationChainToFreshDatabase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BoardOilDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new BoardOilDbContext(options);

        await db.Database.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Contains("Jobs", await GetTableNamesAsync(connection));
        Assert.Contains("JobLogs", await GetTableNamesAsync(connection));
        Assert.Contains("ScheduledJobSchedulerStates", await GetTableNamesAsync(connection));
    }

    private static async Task<List<string>> GetTableNamesAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}
