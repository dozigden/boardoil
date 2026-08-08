using BoardOil.Dev;
using Xunit;

namespace BoardOil.Dev.Tests;

public sealed class DevWebLaunchSettingsTests
{
    [Fact]
    public void CreateEnvironmentShouldUseHttpsApiForOAuthMetadata()
    {
        // Act
        var environment = DevWebLaunchSettings.CreateEnvironment();

        // Assert
        Assert.Equal(
            "https://localhost:5001",
            environment["VITE_BO_OAUTH_PROXY_TARGET"]);
    }
}
