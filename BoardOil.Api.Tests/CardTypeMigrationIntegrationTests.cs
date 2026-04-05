using BoardOil.Ef;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class CardTypeMigrationIntegrationTests
{
    [Fact]
    public async Task AddCardTypesAndCardTypeIdMigration_ShouldBackfillSystemTypesAndCards()
    {
        // Arrange
        var dbPath = CreateDbPath("boardoil-cardtype-migration-tests");
        var options = new DbContextOptionsBuilder<BoardOilDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        await using (var db = new BoardOilDbContext(options))
        {
            await db.Database.MigrateAsync("20260402201137_AddBoardMembersRbac");
        }

        await using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            await connection.OpenAsync();

            var now = DateTime.UtcNow.ToString("O");

            await ExecuteNonQueryAsync(connection, $"INSERT INTO \"Boards\" (\"Id\", \"Name\", \"CreatedAtUtc\", \"UpdatedAtUtc\") VALUES (1, 'Board 1', '{now}', '{now}');");
            await ExecuteNonQueryAsync(connection, $"INSERT INTO \"Boards\" (\"Id\", \"Name\", \"CreatedAtUtc\", \"UpdatedAtUtc\") VALUES (2, 'Board 2', '{now}', '{now}');");
            await ExecuteNonQueryAsync(connection, $"INSERT INTO \"Boards\" (\"Id\", \"Name\", \"CreatedAtUtc\", \"UpdatedAtUtc\") VALUES (3, 'Board 3', '{now}', '{now}');");

            await ExecuteNonQueryAsync(connection, $"INSERT INTO \"Columns\" (\"Id\", \"BoardId\", \"Title\", \"SortKey\", \"CreatedAtUtc\", \"UpdatedAtUtc\") VALUES (11, 1, 'Todo', '00000000000000000010', '{now}', '{now}');");
            await ExecuteNonQueryAsync(connection, $"INSERT INTO \"Columns\" (\"Id\", \"BoardId\", \"Title\", \"SortKey\", \"CreatedAtUtc\", \"UpdatedAtUtc\") VALUES (12, 2, 'Todo', '00000000000000000010', '{now}', '{now}');");

            await ExecuteNonQueryAsync(connection, $"INSERT INTO \"Cards\" (\"Id\", \"BoardColumnId\", \"Title\", \"Description\", \"SortKey\", \"CreatedAtUtc\", \"UpdatedAtUtc\") VALUES (101, 11, 'A', '', '00000000000000000010', '{now}', '{now}');");
            await ExecuteNonQueryAsync(connection, $"INSERT INTO \"Cards\" (\"Id\", \"BoardColumnId\", \"Title\", \"Description\", \"SortKey\", \"CreatedAtUtc\", \"UpdatedAtUtc\") VALUES (102, 11, 'B', '', '00000000000000000020', '{now}', '{now}');");
            await ExecuteNonQueryAsync(connection, $"INSERT INTO \"Cards\" (\"Id\", \"BoardColumnId\", \"Title\", \"Description\", \"SortKey\", \"CreatedAtUtc\", \"UpdatedAtUtc\") VALUES (201, 12, 'C', '', '00000000000000000010', '{now}', '{now}');");
        }

        // Act
        await using (var db = new BoardOilDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        // Assert
        await using var assertConnection = new SqliteConnection($"Data Source={dbPath}");
        await assertConnection.OpenAsync();

        var hasCardTypeId = await ExecuteScalarAsync<long>(
            assertConnection,
            "SELECT COUNT(1) FROM pragma_table_info('Cards') WHERE name = 'CardTypeId';");
        var cardTypeIdNotNull = await ExecuteScalarAsync<long>(
            assertConnection,
            "SELECT \"notnull\" FROM pragma_table_info('Cards') WHERE name = 'CardTypeId';");
        Assert.Equal(1, hasCardTypeId);
        Assert.Equal(1, cardTypeIdNotNull);

        var cardTypeRows = await ExecuteCardTypesAsync(assertConnection);
        Assert.Equal(["1:Story:1", "2:Story:1", "3:Story:1"], cardTypeRows);

        var cardAssignments = await ExecuteCardAssignmentsAsync(assertConnection);
        Assert.Equal(["101:1", "102:1", "201:2"], cardAssignments);
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

    private static async Task<IReadOnlyList<string>> ExecuteCardTypesAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT "BoardId", "Name", "IsSystem"
            FROM "CardTypes"
            ORDER BY "BoardId";
            """;

        var results = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add($"{reader.GetInt32(0)}:{reader.GetString(1)}:{reader.GetInt64(2)}");
        }

        return results;
    }

    private static async Task<IReadOnlyList<string>> ExecuteCardAssignmentsAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT c."Id", ct."BoardId"
            FROM "Cards" c
            INNER JOIN "CardTypes" ct ON ct."Id" = c."CardTypeId"
            ORDER BY c."Id";
            """;

        var results = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add($"{reader.GetInt32(0)}:{reader.GetInt32(1)}");
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
