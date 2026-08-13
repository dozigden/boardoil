using BoardOil.Dev;
using Xunit;

namespace BoardOil.Dev.Tests;

public sealed class DevDemoLaunchSettingsTests
{
    [Fact]
    public void EndpointShouldUseDedicatedDemoPort()
    {
        // Assert
        Assert.Equal(5174, DevDemoLaunchSettings.Port);
        Assert.Equal("http://localhost:5174", DevDemoLaunchSettings.Endpoint);
    }
}
