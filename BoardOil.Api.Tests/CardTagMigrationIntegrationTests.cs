using BoardOil.Ef;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class CardTagMigrationIntegrationTests
{
    [Fact]
    public async Task AddCardTagTagIdForeignKeyMigration_ShouldBackfillAndPreserveCards()
    {
        // Arrange
        var dbPath = CreateDbPath("boardoil-cardtag-migration-tests");
        var options = new DbContextOptionsBuilder<BoardOilDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        await using (var db = new BoardOilDbContext(options))
        {
            await db.Database.MigrateAsync("20260328202823_AddMcpPublicBaseUrlSetting");
        }

        await using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            await connection.OpenAsync();

            var now = DateTime.UtcNow.ToString("O");

            await ExecuteNonQueryAsync(connection, $"INSERT INTO \"Boards\" (\"Id\", \"Name\", \"CreatedAtUtc\", \"UpdatedAtUtc\") VALUES (1, 'BoardOil', '{now}', '{now}');");
            await ExecuteNonQueryAsync(connection, $"INSERT INTO \"Columns\" (\"Id\", \"BoardId\", \"Title\", \"SortKey\", \"CreatedAtUtc\", \"UpdatedAtUtc\") VALUES (1, 1, 'Todo', '00000000000000000010', '{now}', '{now}');");
            await ExecuteNonQueryAsync(connection, $"INSERT INTO \"Cards\" (\"Id\", \"BoardColumnId\", \"Title\", \"Description\", \"SortKey\", \"CreatedAtUtc\", \"UpdatedAtUtc\") VALUES (101, 1, 'Card A', '', '00000000000000000010', '{now}', '{now}');");
            await ExecuteNonQueryAsync(connection, $"INSERT INTO \"Cards\" (\"Id\", \"BoardColumnId\", \"Title\", \"Description\", \"SortKey\", \"CreatedAtUtc\", \"UpdatedAtUtc\") VALUES (102, 1, 'Card B', '', '00000000000000000020', '{now}', '{now}');");

            await ExecuteNonQueryAsync(connection, $"INSERT INTO \"Tags\" (\"Id\", \"Name\", \"NormalisedName\", \"StyleName\", \"StylePropertiesJson\", \"CreatedAtUtc\", \"UpdatedAtUtc\") VALUES (1, 'Bug', 'BUG', 'solid', '{{\"backgroundColor\":\"#224466\",\"textColorMode\":\"auto\"}}', '{now}', '{now}');");

            await ExecuteNonQueryAsync(connection, "INSERT INTO \"CardTags\" (\"Id\", \"CardId\", \"TagName\") VALUES (1, 101, 'Bug');");
            await ExecuteNonQueryAsync(connection, "INSERT INTO \"CardTags\" (\"Id\", \"CardId\", \"TagName\") VALUES (2, 101, 'bug');");
            await ExecuteNonQueryAsync(connection, "INSERT INTO \"CardTags\" (\"Id\", \"CardId\", \"TagName\") VALUES (3, 102, 'LegacyOnly');");
        }

        // Act
        await using (var db = new BoardOilDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        // Assert
        await using var assertConnection = new SqliteConnection($"Data Source={dbPath}");
        await assertConnection.OpenAsync();

        var hasTagId = await ExecuteScalarAsync<long>(
            assertConnection,
            "SELECT COUNT(1) FROM pragma_table_info('CardTags') WHERE name = 'TagId';");
        var hasTagName = await ExecuteScalarAsync<long>(
            assertConnection,
            "SELECT COUNT(1) FROM pragma_table_info('CardTags') WHERE name = 'TagName';");

        Assert.Equal(1, hasTagId);
        Assert.Equal(0, hasTagName);

        var cardCount = await ExecuteScalarAsync<long>(assertConnection, "SELECT COUNT(1) FROM \"Cards\";");
        var cardTagCount = await ExecuteScalarAsync<long>(assertConnection, "SELECT COUNT(1) FROM \"CardTags\";");

        Assert.Equal(2, cardCount);
        Assert.Equal(2, cardTagCount);

        var resolvedTags = await ExecuteTagAssignmentsAsync(assertConnection);
        Assert.Equal(["101:Bug", "102:LegacyOnly"], resolvedTags);

        // Cascade should only remove card-tag links, not cards.
        await ExecuteNonQueryAsync(assertConnection, "DELETE FROM \"Tags\" WHERE \"Name\" = 'Bug';");
        var cardCountAfterTagDelete = await ExecuteScalarAsync<long>(assertConnection, "SELECT COUNT(1) FROM \"Cards\";");
        var cardATagCount = await ExecuteScalarAsync<long>(assertConnection, "SELECT COUNT(1) FROM \"CardTags\" WHERE \"CardId\" = 101;");

        Assert.Equal(2, cardCountAfterTagDelete);
        Assert.Equal(0, cardATagCount);
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ExecuteScalarAsync<T>(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(value!, typeof(T))!;
    }

    private static async Task<IReadOnlyList<string>> ExecuteTagAssignmentsAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT ct."CardId", t."Name"
            FROM "CardTags" ct
            INNER JOIN "Tags" t ON t."Id" = ct."TagId"
            ORDER BY ct."CardId", t."Name";
            """;

        var results = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add($"{reader.GetInt32(0)}:{reader.GetString(1)}");
        }

        return results;
    }

    private static string CreateDbPath(string dbNamePrefix)
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), ".test-data");
        Directory.CreateDirectory(root);
        return Path.Combine(root, $"{dbNamePrefix}-{Guid.NewGuid():N}.db");
    }
}
