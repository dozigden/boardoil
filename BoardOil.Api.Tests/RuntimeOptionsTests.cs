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

    [Fact]
    public void ResolveSigningKeyPath_WithConfiguredDataPath_ShouldUseDataDirectory()
    {
        // Arrange
        var dataPath = Path.Combine("relative-data", "boardoil.db");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BoardOil:DataPath"] = dataPath
            })
            .Build();
        var options = BoardOilRuntimeOptions.FromConfiguration(config);

        // Act
        var signingKeyPath = options.ResolveSigningKeyPath(options.ResolveConnectionString(config));

        // Assert
        var expectedDirectory = Path.GetDirectoryName(Path.GetFullPath(dataPath));
        Assert.Equal(Path.Combine(expectedDirectory!, "boardoil-auth-signing-key"), signingKeyPath);
    }

    [Fact]
    public void ResolveSigningKeyPath_WithExplicitConnectionString_ShouldUseDatabaseDirectory()
    {
        // Arrange
        var databasePath = Path.Combine("connection-data", "boardoil.db");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BoardOil:DataPath"] = Path.Combine("ignored-data", "boardoil.db"),
                ["ConnectionStrings:BoardOil"] = $"Data Source={databasePath};Cache=Shared"
            })
            .Build();
        var options = BoardOilRuntimeOptions.FromConfiguration(config);

        // Act
        var connectionString = options.ResolveConnectionString(config);
        var signingKeyPath = options.ResolveSigningKeyPath(connectionString);

        // Assert
        var expectedDirectory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        Assert.Equal(Path.Combine(expectedDirectory!, "boardoil-auth-signing-key"), signingKeyPath);
    }
}
