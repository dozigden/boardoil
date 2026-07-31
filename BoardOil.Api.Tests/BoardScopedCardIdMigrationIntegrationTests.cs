using BoardOil.Ef;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class BoardScopedCardIdMigrationIntegrationTests
{
    [Fact]
    public async Task AddBoardScopedCardIdFoundationMigration_ShouldBackfillCardsAndSequences()
    {
        // Arrange
        var databasePath = CreateDatabasePath();
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var options = new DbContextOptionsBuilder<BoardOilDbContext>()
            .UseSqlite(connectionString)
            .Options;

        try
        {
            await using (var dbContext = new BoardOilDbContext(options))
            {
                await dbContext.Database.MigrateAsync("20260728145213_AddCardExternalUrl");
            }

            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                var now = DateTime.UtcNow.ToString("O");

                await ExecuteNonQueryAsync(connection,
                    $"INSERT INTO \"Boards\" (\"Id\", \"Name\", \"CreatedAtUtc\", \"UpdatedAtUtc\") VALUES (1, 'First', '{now}', '{now}'), (2, 'Second', '{now}', '{now}'), (3, 'Empty', '{now}', '{now}');");
                await ExecuteNonQueryAsync(connection,
                    $"INSERT INTO \"CardTypes\" (\"Id\", \"BoardId\", \"Name\", \"IsSystem\", \"CreatedAtUtc\", \"UpdatedAtUtc\") VALUES (1, 1, 'Story', 1, '{now}', '{now}'), (2, 2, 'Story', 1, '{now}', '{now}');");
                await ExecuteNonQueryAsync(connection,
                    $"INSERT INTO \"Columns\" (\"Id\", \"BoardId\", \"Title\", \"SortKey\", \"CreatedAtUtc\", \"UpdatedAtUtc\") VALUES (1, 1, 'Todo', 'A', '{now}', '{now}'), (2, 2, 'Todo', 'A', '{now}', '{now}');");
                await ExecuteNonQueryAsync(connection,
                    $"INSERT INTO \"Cards\" (\"Id\", \"BoardColumnId\", \"CardTypeId\", \"Title\", \"Description\", \"SortKey\", \"CreatedAtUtc\", \"UpdatedAtUtc\") VALUES (11, 1, 1, 'First card', '', 'A', '{now}', '{now}'), (21, 2, 2, 'Second card', '', 'A', '{now}', '{now}');");
                await ExecuteNonQueryAsync(connection,
                    $"INSERT INTO \"ArchivedCards\" (\"BoardId\", \"OriginalCardId\", \"ArchivedAtUtc\", \"SnapshotJson\", \"SearchTitle\", \"SearchTagsJson\", \"SearchTextNormalised\") VALUES (1, 50, '{now}', '{{}}', 'Archived first', '[]', 'ARCHIVED FIRST'), (2, 7, '{now}', '{{}}', 'Archived second', '[]', 'ARCHIVED SECOND'), (1, 11, '{now}', '{{}}', 'Colliding archive', '[]', 'COLLIDING ARCHIVE'), (2, -4, '{now}', '{{}}', 'Invalid archive', '[]', 'INVALID ARCHIVE');");
            }

            // Act
            await using (var dbContext = new BoardOilDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
            }

            // Assert
            await using var assertConnection = new SqliteConnection(connectionString);
            await assertConnection.OpenAsync();

            var cards = await QueryRowsAsync(
                assertConnection,
                "SELECT \"Id\", \"BoardId\", \"BoardCardId\" FROM \"Cards\" ORDER BY \"Id\";");
            Assert.Equal(["11:1:11", "21:2:21"], cards);

            var sequences = await QueryRowsAsync(
                assertConnection,
                "SELECT \"BoardId\", \"NextCardId\" FROM \"BoardCardIdSequences\" ORDER BY \"BoardId\";");
            Assert.Equal(["1:52", "2:23", "3:1"], sequences);

            var archivedCards = await QueryRowsAsync(
                assertConnection,
                "SELECT \"BoardId\", \"OriginalCardId\" FROM \"ArchivedCards\" ORDER BY \"Id\";");
            Assert.Equal(["1:50", "2:7", "1:51", "2:22"], archivedCards);

            await ExecuteNonQueryAsync(assertConnection,
                "INSERT INTO \"ArchivedCards\" (\"BoardId\", \"OriginalCardId\", \"ArchivedAtUtc\", \"SnapshotJson\", \"SearchTitle\", \"SearchTagsJson\", \"SearchTextNormalised\") VALUES (1, 7, '2026-07-31T00:00:00Z', '{}', 'Cross-board duplicate', '[]', 'CROSS-BOARD DUPLICATE');");
            await Assert.ThrowsAsync<SqliteException>(() => ExecuteNonQueryAsync(assertConnection,
                "INSERT INTO \"ArchivedCards\" (\"BoardId\", \"OriginalCardId\", \"ArchivedAtUtc\", \"SnapshotJson\", \"SearchTitle\", \"SearchTagsJson\", \"SearchTextNormalised\") VALUES (1, 7, '2026-07-31T00:00:01Z', '{}', 'Same-board duplicate', '[]', 'SAME-BOARD DUPLICATE');"));

            await ExecuteNonQueryAsync(assertConnection,
                "INSERT INTO \"Cards\" (\"BoardId\", \"BoardCardId\", \"BoardColumnId\", \"CardTypeId\", \"Title\", \"Description\", \"SortKey\", \"CreatedAtUtc\", \"UpdatedAtUtc\") VALUES (2, 11, 2, 2, 'Cross-board card ID', '', 'B', '2026-07-31T00:00:00Z', '2026-07-31T00:00:00Z');");
            await Assert.ThrowsAsync<SqliteException>(() => ExecuteNonQueryAsync(assertConnection,
                "INSERT INTO \"Cards\" (\"BoardId\", \"BoardCardId\", \"BoardColumnId\", \"CardTypeId\", \"Title\", \"Description\", \"SortKey\", \"CreatedAtUtc\", \"UpdatedAtUtc\") VALUES (2, 11, 2, 2, 'Same-board duplicate', '', 'C', '2026-07-31T00:00:01Z', '2026-07-31T00:00:01Z');"));
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task AddBoardScopedCardIdFoundationMigration_WhenDowngraded_ShouldDirectToBackupRestore()
    {
        // Arrange
        var databasePath = CreateDatabasePath();
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var options = new DbContextOptionsBuilder<BoardOilDbContext>()
            .UseSqlite(connectionString)
            .Options;

        try
        {
            await using var dbContext = new BoardOilDbContext(options);
            await dbContext.Database.MigrateAsync();

            // Act
            var exception = await Assert.ThrowsAsync<NotSupportedException>(
                () => dbContext.Database.MigrateAsync("20260728145213_AddCardExternalUrl"));

            // Assert
            Assert.Equal(
                "Board-scoped card IDs cannot be migrated back to globally unique card IDs. "
                    + "Restore the automatic pre-migration database backup instead.",
                exception.Message);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<IReadOnlyList<string>> QueryRowsAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(string.Join(':', Enumerable.Range(0, reader.FieldCount).Select(index => reader.GetInt32(index))));
        }

        return rows;
    }

    private static string CreateDatabasePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "boardoil-board-card-id-migration-tests");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{Guid.NewGuid():N}.db");
    }
}
