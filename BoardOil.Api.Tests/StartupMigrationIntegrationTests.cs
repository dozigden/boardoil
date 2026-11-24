using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Ef;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class StartupMigrationIntegrationTests
{
    [Fact]
    public async Task Startup_ShouldUpgradeExistingDataAndLeaveBackupFilesAlone()
    {
        var directory = Directory.CreateTempSubdirectory("boardoil-startup-migration-");
        var dbPath = Path.Combine(directory.FullName, "boardoil.db");
        var connectionString = $"Data Source={dbPath}";
        var options = new DbContextOptionsBuilder<BoardOilDbContext>().UseSqlite(connectionString).Options;
        try
        {
            await using (var db = new BoardOilDbContext(options))
            {
                await db.Database.MigrateAsync("20261109151910_UseSingleUniqueUserEmail");
                await db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO Users (UserName, DisplayName, Email, Role, IdentityType, IsActive, CreatedAtUtc, UpdatedAtUtc)
                    VALUES ('lee', 'Lee', 'lee@example.test', 0, 0, 1, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
                    INSERT INTO Boards (Name, Description, CreatedAtUtc, UpdatedAtUtc)
                    VALUES ('Workshop', 'Tools and supplies', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
                    """);
            }

            var backups = directory.CreateSubdirectory("backups");
            var legacyFile = Path.Combine(backups.FullName, "boardoil-backup-2000-01-01T00-00-00Z.db");
            await File.WriteAllTextAsync(legacyFile, "existing copy");

            await using var factory = new BoardOilApiFactory(dbPath);
            using var client = factory.CreateClient();
            using var response = await client.GetAsync("/api/health");
            response.EnsureSuccessStatusCode();

            await using var upgraded = new BoardOilDbContext(options);
            Assert.Empty(await upgraded.Database.GetPendingMigrationsAsync());
            var user = await upgraded.Users.SingleAsync(user => user.UserName == "lee");
            Assert.Equal("lee@example.test", user.Email);
            var board = await upgraded.Boards.SingleAsync(board => board.Name == "Workshop");
            Assert.Equal("Tools and supplies", board.Description);
            Assert.Equal(legacyFile, Assert.Single(Directory.GetFiles(backups.FullName)));
            Assert.Equal("existing copy", await File.ReadAllTextAsync(legacyFile));
        }
        finally
        {
            using var connection = new SqliteConnection(connectionString);
            SqliteConnection.ClearPool(connection);
            directory.Delete(recursive: true);
        }
    }
}
