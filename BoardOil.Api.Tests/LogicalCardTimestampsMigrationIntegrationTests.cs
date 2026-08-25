using BoardOil.Ef;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class LogicalCardTimestampsMigrationIntegrationTests
{
    [Fact]
    public async Task AddLogicalCardTimestampsMigration_ShouldBackfillExistingCardsFromRowMetadata()
    {
        var dbPath = CreateDbPath();
        var options = new DbContextOptionsBuilder<BoardOilDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        const string targetMigration = "20260825181214_AddLogicalCardTimestamps";

        await using (var db = new BoardOilDbContext(options))
        {
            var migrations = db.Database.GetMigrations().ToList();
            var targetIndex = migrations.FindIndex(static migration => migration == targetMigration);
            Assert.True(targetIndex > 0);
            await db.Database.MigrateAsync(migrations[targetIndex - 1]);
        }

        var createdAtUtc = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc);
        var updatedAtUtc = createdAtUtc.AddDays(2);
        await using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            await connection.OpenAsync();
            await ExecuteNonQueryAsync(
                connection,
                $$"""
                INSERT INTO "Boards" ("Id", "Name", "Description", "SlickCohesionModeEnabled", "CreatedAtUtc", "UpdatedAtUtc")
                VALUES (1, 'Board', '', 1, '{{createdAtUtc:O}}', '{{updatedAtUtc:O}}');

                INSERT INTO "CardTypes" ("Id", "BoardId", "Name", "Emoji", "StyleName", "StylePropertiesJson", "IsSystem", "CreatedAtUtc", "UpdatedAtUtc")
                VALUES (1, 1, 'Story', NULL, 'auto', '{}', 1, '{{createdAtUtc:O}}', '{{updatedAtUtc:O}}');

                INSERT INTO "Columns" ("Id", "BoardId", "Title", "SortKey", "CreatedAtUtc", "UpdatedAtUtc")
                VALUES (1, 1, 'Todo', 'A', '{{createdAtUtc:O}}', '{{updatedAtUtc:O}}');

                INSERT INTO "Cards" ("Id", "BoardId", "BoardCardId", "BoardColumnId", "CardTypeId", "Title", "Description", "SortKey", "CreatedAtUtc", "UpdatedAtUtc")
                VALUES (1, 1, 1, 1, 1, 'Card', '', 'A', '{{createdAtUtc:O}}', '{{updatedAtUtc:O}}');
                """);
        }

        await using (var db = new BoardOilDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        await using var assertConnection = new SqliteConnection($"Data Source={dbPath}");
        await assertConnection.OpenAsync();
        await using var command = assertConnection.CreateCommand();
        command.CommandText = "SELECT \"CardCreatedUtc\", \"CardUpdatedUtc\" FROM \"Cards\" WHERE \"Id\" = 1;";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(createdAtUtc, reader.GetDateTime(0));
        Assert.Equal(updatedAtUtc, reader.GetDateTime(1));
        await reader.DisposeAsync();

        command.CommandText =
            "SELECT \"name\", \"notnull\" FROM pragma_table_info('Cards') WHERE \"name\" IN ('CardCreatedUtc', 'CardUpdatedUtc');";
        await using var schemaReader = await command.ExecuteReaderAsync();
        var logicalTimestampColumns = new Dictionary<string, bool>();
        while (await schemaReader.ReadAsync())
        {
            logicalTimestampColumns[schemaReader.GetString(0)] = schemaReader.GetBoolean(1);
        }

        Assert.Equal(2, logicalTimestampColumns.Count);
        Assert.All(logicalTimestampColumns.Values, Assert.True);
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static string CreateDbPath()
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), ".test-data");
        Directory.CreateDirectory(root);
        return Path.Combine(root, $"boardoil-logical-card-timestamps-migration-tests-{Guid.NewGuid():N}.db");
    }
}
