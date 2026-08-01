using BoardOil.Dev;
using Xunit;

namespace BoardOil.Dev.Tests;

public sealed class ManagedServiceStateTests
{
    [Theory]
    [InlineData(false, false, null, false, "stopped", false)]
    [InlineData(true, true, null, false, "running", false)]
    [InlineData(true, false, 0, false, "stopped", false)]
    [InlineData(true, false, 137, true, "stopped", false)]
    [InlineData(true, false, 2, false, "exit 2", true)]
    public void ResolveShouldDescribeProcessState(
        bool hasProcess,
        bool isRunning,
        int? exitCode,
        bool stoppedByUser,
        string expectedText,
        bool expectedFailure)
    {
        // Act
        var result = ServiceProcessState.Resolve(hasProcess, isRunning, exitCode, stoppedByUser);

        // Assert
        Assert.Equal(expectedText, result.Text);
        Assert.Equal(expectedFailure, result.HasFailed);
    }

    [Fact]
    public void RecentLogBufferShouldKeepCapacityAndReturnTail()
    {
        // Arrange
        var buffer = new RecentLogBuffer(3);
        buffer.Add("one");
        buffer.Add("two");
        buffer.Add("three");
        buffer.Add("four");

        // Act
        var result = buffer.Tail(2);

        // Assert
        Assert.Equal(3, buffer.Count);
        Assert.Equal(["three", "four"], result);
    }

    [Theory]
    [InlineData("dotnet BoardOil.Api", new[] { "BoardOil.Api" }, true)]
    [InlineData("node vite --port 5173", new[] { "vite" }, true)]
    [InlineData("node another-server", new[] { "vite" }, false)]
    public void IsRecognisedShouldRequireEveryCommandFragment(
        string commandLine,
        string[] fragments,
        bool expected)
    {
        // Act
        var result = PortConflictResolver.IsRecognised(commandLine, fragments);

        // Assert
        Assert.Equal(expected, result);
    }
}
