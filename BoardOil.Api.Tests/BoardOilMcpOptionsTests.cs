using BoardOil.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class BoardOilMcpOptionsTests
{
    [Fact]
    public void FromConfiguration_WhenSectionMissing_ShouldUseSecureDefaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var options = BoardOilMcpOptions.FromConfiguration(configuration);

        Assert.Equal(McpTransportMode.Http, options.TransportMode);
        Assert.Equal(McpAuthMode.Pat, options.AuthMode);
        Assert.Equal(1, options.AnonymousActorUserId);
        Assert.False(options.SupportsLegacySseTransport);
    }

    [Fact]
    public void FromConfiguration_WhenTransportAndAuthConfigured_ShouldParseCaseInsensitiveValues()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BoardOilMcp:TransportMode"] = "BoTh",
                ["BoardOilMcp:AuthMode"] = "NoNe",
                ["BoardOilMcp:AnonymousActorUserId"] = "42"
            })
            .Build();

        var options = BoardOilMcpOptions.FromConfiguration(configuration);

        Assert.Equal(McpTransportMode.Both, options.TransportMode);
        Assert.Equal(McpAuthMode.None, options.AuthMode);
        Assert.Equal(42, options.AnonymousActorUserId);
        Assert.True(options.SupportsLegacySseTransport);
    }

    [Fact]
    public void FromConfiguration_WhenTransportModeUsesSseAlias_ShouldMapToBoth()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BoardOilMcp:TransportMode"] = "sSe"
            })
            .Build();

        var options = BoardOilMcpOptions.FromConfiguration(configuration);

        Assert.Equal(McpTransportMode.Both, options.TransportMode);
        Assert.True(options.SupportsLegacySseTransport);
    }
}
