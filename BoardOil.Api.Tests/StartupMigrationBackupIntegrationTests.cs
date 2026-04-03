using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Ef;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class StartupMigrationBackupIntegrationTests
{
    [Fact]
    public async Task StartupMigration_ShouldCreateBackupAndDeleteExpiredBackups()
    {
        // Arrange
        var dbPath = CreateDbPath("boardoil-startup-migration-backup-tests");
        var options = new DbContextOptionsBuilder<BoardOilDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        await using (var db = new BoardOilDbContext(options))
        {
            var migrations = db.Database.GetMigrations().ToList();
            Assert.True(migrations.Count > 1, "Expected at least two migrations for backup-before-migrate test.");
            var migrationBeforeLatest = migrations[^2];
            await db.Database.MigrateAsync(migrationBeforeLatest);
        }

        var dbDirectory = Path.GetDirectoryName(dbPath);
        Assert.False(string.IsNullOrWhiteSpace(dbDirectory));

        var backupDirectory = Path.Combine(dbDirectory!, "backups");
        Directory.CreateDirectory(backupDirectory);
        var staleBackupPath = Path.Combine(backupDirectory, "boardoil-backup-2000-01-01T00-00-00Z.db");
        await File.WriteAllTextAsync(staleBackupPath, "stale");

        // Act
        await using (var factory = new BoardOilApiFactory(dbPath))
        using (var client = factory.CreateClient())
        {
            var response = await client.GetAsync("/api/health");
            response.EnsureSuccessStatusCode();
        }

        // Assert
        Assert.False(File.Exists(staleBackupPath));

        var backupFiles = Directory.EnumerateFiles(backupDirectory, "boardoil-backup-*.db").ToList();
        Assert.Single(backupFiles);
        Assert.Matches(@"boardoil-backup-\d{4}-\d{2}-\d{2}T\d{2}-\d{2}-\d{2}(\.\d{7})?Z\.db", Path.GetFileName(backupFiles[0]));

        await using var assertDb = new BoardOilDbContext(options);
        var pending = await assertDb.Database.GetPendingMigrationsAsync();
        Assert.Empty(pending);
    }

    private static string CreateDbPath(string dbNamePrefix)
    {
        var root = Path.Combine(
            Directory.GetCurrentDirectory(),
            ".test-data",
            $"{dbNamePrefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return Path.Combine(root, "boardoil.db");
    }
}
