using BoardOil.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class RuntimeOptionsTests
{
    [Fact]
    public void ResolveListenUrl_WhenNoAspNetCoreUrlsAndExposeLanFalse_ShouldUseLocalhost()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BoardOil:ExposeLan"] = "false",
                ["BoardOil:Port"] = "5000"
            })
            .Build();

        var options = BoardOilRuntimeOptions.FromConfiguration(config);

        var url = options.ResolveListenUrl(config);

        Assert.Equal("http://127.0.0.1:5000", url);
    }

    [Fact]
    public void ResolveListenUrl_WhenAspNetCoreUrlsSet_ShouldHonorExplicitOverride()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BoardOil:ExposeLan"] = "false",
                ["BoardOil:Port"] = "5000",
                ["ASPNETCORE_URLS"] = "http://0.0.0.0:6000"
            })
            .Build();

        var options = BoardOilRuntimeOptions.FromConfiguration(config);

        var url = options.ResolveListenUrl(config);

        Assert.Equal("http://0.0.0.0:6000", url);
    }
}
