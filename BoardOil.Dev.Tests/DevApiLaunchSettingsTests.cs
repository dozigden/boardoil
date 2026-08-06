using BoardOil.Dev;
using Xunit;

namespace BoardOil.Dev.Tests;

public sealed class DevApiLaunchSettingsTests
{
    [Fact]
    public void CreateEnvironmentShouldExposeHttpAndHttpsForLocalDevelopment()
    {
        // Arrange
        const string databasePath = "/tmp/boardoil.dev.db";

        // Act
        var environment = DevApiLaunchSettings.CreateEnvironment(databasePath);

        // Assert
        Assert.Equal(
            "http://127.0.0.1:5000;https://localhost:5001",
            environment["ASPNETCORE_URLS"]);
        Assert.Equal("Development", environment["ASPNETCORE_ENVIRONMENT"]);
        Assert.Equal("Development", environment["DOTNET_ENVIRONMENT"]);
        Assert.Equal($"Data Source={databasePath}", environment["ConnectionStrings__BoardOil"]);
        Assert.Equal([5000, 5001], DevApiLaunchSettings.Ports);
    }

    [Fact]
    public void CreateRunArgumentsShouldLeaveEndpointSelectionToEnvironment()
    {
        // Arrange
        const string apiProject = "/repo/BoardOil.Api/BoardOil.Api.csproj";

        // Act
        var arguments = DevApiLaunchSettings.CreateRunArguments(apiProject);

        // Assert
        Assert.Equal(
            ["run", "--no-launch-profile", "--no-build", "--project", apiProject],
            arguments);
        Assert.DoesNotContain("--urls", arguments);
    }
}
