using BoardOil.Ef;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class CardChecklistCountsMigrationIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChecklistCountsMigrations_ShouldPreserveExistingCards(bool initialCountsAlreadyApplied)
    {
        var dbPath = CreateDbPath();
        var options = new DbContextOptionsBuilder<BoardOilDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        const string targetMigration = "20260926144813_AddCardTaskProgress";

        await using (var db = new BoardOilDbContext(options))
        {
            var migrations = db.Database.GetMigrations().ToList();
            var targetIndex = migrations.FindIndex(static migration => migration == targetMigration);
            Assert.True(targetIndex > 0);
            var baselineMigration = migrations[targetIndex - 1];
            if (initialCountsAlreadyApplied) { baselineMigration = targetMigration; }
            await db.Database.MigrateAsync(baselineMigration);
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

                INSERT INTO "Cards" ("Id", "BoardId", "BoardCardId", "BoardColumnId", "CardTypeId", "Title", "Description", "SortKey", "CreatedAtUtc", "UpdatedAtUtc", "CardCreatedUtc", "CardUpdatedUtc")
                VALUES (1, 1, 1, 1, 1, 'Card', '- [x] Existing task', 'A', '{{createdAtUtc:O}}', '{{updatedAtUtc:O}}', '{{createdAtUtc:O}}', '{{updatedAtUtc:O}}');
                """);
            if (initialCountsAlreadyApplied)
            {
                // Renaming preserves stored values without recalculating descriptions.
                await ExecuteNonQueryAsync(connection, """
                    UPDATE "Cards" SET "CompletedTaskCount" = 2, "TotalTaskCount" = 3 WHERE "Id" = 1;
                    """);
            }
        }

        await using (var db = new BoardOilDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        await using var assertConnection = new SqliteConnection($"Data Source={dbPath}");
        await assertConnection.OpenAsync();
        await using var command = assertConnection.CreateCommand();
        command.CommandText = """
            SELECT "CompletedChecklistItemCount", "TotalChecklistItemCount", "Description", "CardCreatedUtc", "CardUpdatedUtc", "UpdatedAtUtc"
            FROM "Cards" WHERE "Id" = 1;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(initialCountsAlreadyApplied ? 2 : 0, reader.GetInt32(0));
        Assert.Equal(initialCountsAlreadyApplied ? 3 : 0, reader.GetInt32(1));
        Assert.Equal("- [x] Existing task", reader.GetString(2));
        Assert.Equal(createdAtUtc, reader.GetDateTime(3));
        Assert.Equal(updatedAtUtc, reader.GetDateTime(4));
        Assert.Equal(updatedAtUtc, reader.GetDateTime(5));
        await reader.DisposeAsync();

        command.CommandText = """
            SELECT "name", "notnull", "dflt_value" FROM pragma_table_info('Cards')
            WHERE "name" IN ('CompletedChecklistItemCount', 'TotalChecklistItemCount');
            """;
        await using var schemaReader = await command.ExecuteReaderAsync();
        var columns = 0;
        while (await schemaReader.ReadAsync())
        {
            columns++;
            Assert.True(schemaReader.GetBoolean(1));
            Assert.Equal("0", schemaReader.GetString(2));
        }
        Assert.Equal(2, columns);
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
        return Path.Combine(root, $"boardoil-checklist-counts-migration-tests-{Guid.NewGuid():N}.db");
    }
}
