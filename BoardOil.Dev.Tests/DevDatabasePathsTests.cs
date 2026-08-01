using BoardOil.Dev;
using Xunit;

namespace BoardOil.Dev.Tests;

public sealed class DevDatabasePathsTests
{
    [Fact]
    public void ResolveBranchDatabasePathShouldUseSharedPathOnMainBranch()
    {
        // Arrange
        var repoRoot = Path.Combine(Path.DirectorySeparatorChar.ToString(), "repo");

        // Act
        var result = DevDatabasePaths.ResolveBranchDatabasePath(repoRoot, "main", "main");

        // Assert
        Assert.Equal(Path.Combine(repoRoot, ".data", "dev", "boardoil.dev.db"), result);
    }

    [Fact]
    public void ResolveBranchDatabasePathShouldUseSanitisedBranchDirectory()
    {
        // Arrange
        var repoRoot = Path.Combine(Path.DirectorySeparatorChar.ToString(), "repo");

        // Act
        var result = DevDatabasePaths.ResolveBranchDatabasePath(
            repoRoot,
            "feature/702 local orchestrator",
            "main");

        // Assert
        Assert.Equal(
            Path.Combine(
                repoRoot,
                ".data",
                "dev",
                "branches",
                "feature_702_local_orchestrator",
                "boardoil.dev.db"),
            result);
    }

    [Theory]
    [InlineData("feature///thing", "feature_thing")]
    [InlineData("___", "unknown")]
    [InlineData("detached-a1b2c3d", "detached-a1b2c3d")]
    public void SanitiseBranchNameShouldProduceStableDirectoryName(string branchName, string expected)
    {
        // Act
        var result = DevDatabasePaths.SanitiseBranchName(branchName);

        // Assert
        Assert.Equal(expected, result);
    }
}
