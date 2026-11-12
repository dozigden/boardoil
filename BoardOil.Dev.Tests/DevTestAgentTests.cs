using BoardOil.Dev;
using Xunit;

namespace BoardOil.Dev.Tests;

public sealed class DevTestAgentTests
{
    [Fact]
    public void FindBundledCodexShouldUseTheSelectedDesktopPackage()
    {
        // Arrange
        var directory = Directory.CreateTempSubdirectory("boardoil-codex-discovery-").FullName;
        var package = Path.Combine(directory, "packages", "app-server-daemon", "current");
        Directory.CreateDirectory(package);
        var executable = "codex";
        if (OperatingSystem.IsWindows())
        {
            executable += ".exe";
        }
        var path = Path.Combine(package, executable);
        File.WriteAllText(path, string.Empty);
        try
        {
            // Act
            var found = DevTestAgent.FindBundledCodex(directory);

            // Assert
            Assert.Equal(path, found);
            Assert.Null(DevTestAgent.FindBundledCodex(Path.Combine(directory, "missing")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CreateStartInfoShouldIsolateTheSessionAndKeepTheTokenOutOfArguments()
    {
        // Arrange
        const string token = "bo_pat_secret-test-value";
        const string workspace = "/tmp/boardoil agent/workspace";

        // Act
        var startInfo = DevTestAgent.CreateStartInfo("codex", workspace, "boardoil_dev_test",
            [new("boardoil_boardoil", false), new("other.server", true), new("quoted\"server", false)], token);

        // Assert
        var arguments = startInfo.ArgumentList.ToArray();
        Assert.Equal(workspace, startInfo.WorkingDirectory);
        Assert.False(startInfo.UseShellExecute);
        Assert.Contains("--no-daemon", arguments);
        Assert.Contains("read-only", arguments);
        var servers = Assert.Single(arguments, argument => argument.StartsWith("mcp_servers=", StringComparison.Ordinal));
        Assert.Contains("\"boardoil_boardoil\"={url=\"http://127.0.0.1:5000/mcp\",enabled=false}", servers);
        Assert.Contains("\"other.server\"={command=\"false\",enabled=false}", servers);
        Assert.Contains("\"quoted\\u0022server\"={url=\"http://127.0.0.1:5000/mcp\",enabled=false}", servers);
        Assert.Contains("\"boardoil_dev_test\"={url=\"http://127.0.0.1:5000/mcp\"", servers);
        Assert.Contains("required=true", servers);
        Assert.DoesNotContain(arguments, argument => argument.Contains(token, StringComparison.Ordinal));
        Assert.Equal(token, startInfo.Environment[DevTestAgent.TokenEnvironmentVariable]);
        Assert.False(startInfo.RedirectStandardInput);
        Assert.False(startInfo.RedirectStandardOutput);
    }
}
