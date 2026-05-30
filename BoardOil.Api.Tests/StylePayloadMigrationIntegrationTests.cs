using BoardOil.Ef;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class StylePayloadMigrationIntegrationTests
{
    [Fact]
    public async Task NormaliseLegacyStylePropertiesJsonPayloadsMigration_ShouldCanonicaliseKnownStylePayloads()
    {
        // Arrange
        var dbPath = CreateDbPath("boardoil-style-payload-migration-tests");
        var options = new DbContextOptionsBuilder<BoardOilDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        const string targetMigration = "20260530200000_NormaliseLegacyStylePropertiesJsonPayloads";
        await using (var db = new BoardOilDbContext(options))
        {
            var migrations = db.Database.GetMigrations().ToList();
            var targetIndex = migrations.FindIndex(static x => x == targetMigration);
            Assert.True(targetIndex > 0, $"Expected migration '{targetMigration}' to exist after at least one prior migration.");
            await db.Database.MigrateAsync(migrations[targetIndex - 1]);
        }

        await using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            await connection.OpenAsync();

            var now = DateTime.UtcNow.ToString("O");

            await ExecuteNonQueryAsync(connection, $@"
                INSERT INTO ""Boards"" (""Id"", ""Name"", ""Description"", ""CreatedAtUtc"", ""UpdatedAtUtc"")
                VALUES (1, 'Board 1', '', '{now}', '{now}');");

            await ExecuteNonQueryAsync(connection, $@"
                INSERT INTO ""Tags"" (""Id"", ""BoardId"", ""Name"", ""NormalisedName"", ""StyleName"", ""StylePropertiesJson"", ""Emoji"", ""CreatedAtUtc"", ""UpdatedAtUtc"")
                VALUES
                    (1, 1, 'AutoTag', 'AUTOTAG', 'AUTO', '{{""textColorMode"":""custom"",""textColor"":""#FF00FF""}}', NULL, '{now}', '{now}'),
                    (2, 1, 'PresetTag', 'PRESETTAG', 'presets', '{{""presetIndex"":""5"",""textColorMode"":""custom""}}', NULL, '{now}', '{now}'),
                    (3, 1, 'SolidTag', 'SOLIDTAG', 'solid', '{{""backgroundColor"":""#69c1ce"",""textColorMode"":""custom"",""borderMode"":""custom""}}', NULL, '{now}', '{now}'),
                    (4, 1, 'GradientTag', 'GRADIENTTAG', 'gradient', '{{""backgroundColor"":""#9bbef8"",""textColorMode"":""auto""}}', NULL, '{now}', '{now}'),
                    (5, 1, 'BrokenGradientTag', 'BROKENGRADIENTTAG', 'gradient', '{{not json', NULL, '{now}', '{now}');");

            await ExecuteNonQueryAsync(connection, $@"
                INSERT INTO ""CardTypes"" (""Id"", ""BoardId"", ""Name"", ""Emoji"", ""StyleName"", ""StylePropertiesJson"", ""IsSystem"", ""CreatedAtUtc"", ""UpdatedAtUtc"")
                VALUES
                    (1, 1, 'Story', NULL, 'auto', '{{}}', 1, '{now}', '{now}'),
                    (2, 1, 'PresetType', NULL, 'presets', '{{""presetIndex"":""banana""}}', 0, '{now}', '{now}'),
                    (3, 1, 'SolidFromLeft', NULL, 'solid', '{{""leftColor"":""#F17437"",""textColorMode"":""auto""}}', 0, '{now}', '{now}');");
        }

        // Act
        await using (var db = new BoardOilDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        // Assert
        await using var assertConnection = new SqliteConnection($"Data Source={dbPath}");
        await assertConnection.OpenAsync();

        var tagStyles = await ExecuteStyleRowsAsync(assertConnection, "Tags");
        Assert.Equal(
            [
                "auto:{}",
                "presets:{\"presetIndex\":5}",
                "solid:{\"backgroundColor\":\"#69C1CE\",\"textColorMode\":\"custom\",\"borderMode\":\"custom\",\"textColor\":\"#111827\",\"borderColor\":\"#D8CDEC\"}",
                "gradient:{\"leftColor\":\"#9BBEF8\",\"rightColor\":\"#9BBEF8\",\"textColorMode\":\"auto\",\"borderMode\":\"auto\"}",
                "gradient:{\"leftColor\":\"#69C1CE\",\"rightColor\":\"#69C1CE\",\"textColorMode\":\"auto\",\"borderMode\":\"auto\"}"
            ],
            tagStyles);

        var cardTypeStyles = await ExecuteStyleRowsAsync(assertConnection, "CardTypes");
        Assert.Equal(
            [
                "auto:{}",
                "presets:{\"presetIndex\":2}",
                "solid:{\"backgroundColor\":\"#F17437\",\"textColorMode\":\"auto\",\"borderMode\":\"auto\"}"
            ],
            cardTypeStyles);
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<IReadOnlyList<string>> ExecuteStyleRowsAsync(SqliteConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT "StyleName", "StylePropertiesJson"
            FROM "{tableName}"
            ORDER BY "Id";
            """;

        var results = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add($"{reader.GetString(0)}:{reader.GetString(1)}");
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
