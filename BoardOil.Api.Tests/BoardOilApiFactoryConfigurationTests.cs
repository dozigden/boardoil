using BoardOil.Api.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class BoardOilApiFactoryConfigurationTests : ApiFactoryIntegrationTestBase
{
    [Fact]
    public void ConfigurationFiles_ShouldLoadWithoutReloadWatchers()
    {
        var configuration = Assert.IsAssignableFrom<IConfigurationRoot>(
            Factory.Services.GetRequiredService<IConfiguration>());

        var fileProviders = configuration.Providers.OfType<FileConfigurationProvider>().ToArray();

        Assert.NotEmpty(fileProviders);
        Assert.All(fileProviders, provider => Assert.False(provider.Source.ReloadOnChange));
    }
}
