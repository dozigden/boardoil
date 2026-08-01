using BoardOil.Dev;
using Xunit;

namespace BoardOil.Dev.Tests;

public sealed class DevDatabaseSeederTests
{
    [Fact]
    public void SeedIfNeededShouldCopyDatabaseAndSqliteSidecars()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var source = Path.Combine(directory.Path, "main", "boardoil.dev.db");
        var target = Path.Combine(directory.Path, "branch", "boardoil.dev.db");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllText(source, "database");
        File.WriteAllText($"{source}-wal", "wal");
        File.WriteAllText($"{source}-shm", "shm");

        // Act
        DevDatabaseSeeder.SeedIfNeeded(source, target, enabled: true, preferSqliteBackup: false);

        // Assert
        Assert.Equal("database", File.ReadAllText(target));
        Assert.Equal("wal", File.ReadAllText($"{target}-wal"));
        Assert.Equal("shm", File.ReadAllText($"{target}-shm"));
    }

    [Fact]
    public void SeedIfNeededShouldNotOverwriteExistingBranchDatabase()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var source = Path.Combine(directory.Path, "main.db");
        var target = Path.Combine(directory.Path, "branch.db");
        File.WriteAllText(source, "main");
        File.WriteAllText(target, "existing");

        // Act
        DevDatabaseSeeder.SeedIfNeeded(source, target, enabled: true);

        // Assert
        Assert.Equal("existing", File.ReadAllText(target));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = Directory.CreateTempSubdirectory("boardoil-dev-tests-").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
