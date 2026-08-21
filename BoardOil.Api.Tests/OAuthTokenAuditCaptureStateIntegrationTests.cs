using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.OAuth;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class OAuthTokenAuditCaptureStateIntegrationTests
{
    [Fact]
    public async Task ApplicationRestart_ShouldLoadPersistedCaptureState()
    {
        // Arrange
        var databasePath = ApiFactoryIntegrationTestBase.BuildDbPath(
            nameof(OAuthTokenAuditCaptureStateIntegrationTests));
        await using (var initialFactory = new BoardOilApiFactory(databasePath))
        {
            using var client = initialFactory.CreateClient();
            await using var dbContext = CreateDbContext(initialFactory.Services);
            dbContext.AppSettings.Add(new EntityAppSetting
            {
                Key = "oauth_lifecycle_diagnostics_enabled",
                Value = "True"
            });
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        await using var restartedFactory = new BoardOilApiFactory(databasePath);
        using var restartedClient = restartedFactory.CreateClient();
        var captureState = restartedFactory.Services
            .GetRequiredService<OAuthTokenAuditCaptureState>();

        // Assert
        Assert.True(captureState.IsEnabled);
    }

    private static BoardOilDbContext CreateDbContext(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory>();
        return factory.CreateDbContext<BoardOilDbContext>();
    }
}
