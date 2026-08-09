using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.ErrorLogs;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class ErrorLogStartupCleanupIntegrationTests
{
    [Fact]
    public async Task ApplicationStartup_ShouldPurgeExpiredErrorLogs()
    {
        // Arrange
        var databasePath = ApiFactoryIntegrationTestBase.BuildDbPath(nameof(ErrorLogStartupCleanupIntegrationTests));
        await using (var initialFactory = new BoardOilApiFactory(databasePath))
        {
            using var client = initialFactory.CreateClient();
            await using var dbContext = CreateDbContext(initialFactory.Services);
            dbContext.ErrorLogs.AddRange(
                NewErrorLog("old", DateTime.UtcNow.AddDays(-ErrorLogRetention.RetentionDays).AddMinutes(-1)),
                NewErrorLog("kept", DateTime.UtcNow));
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        await using var restartedFactory = new BoardOilApiFactory(databasePath);
        using var restartedClient = restartedFactory.CreateClient();
        await using var assertDbContext = CreateDbContext(restartedFactory.Services);
        var remaining = await assertDbContext.ErrorLogs
            .Select(x => x.Message)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["kept"], remaining);
    }

    private static BoardOilDbContext CreateDbContext(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory>();
        return factory.CreateDbContext<BoardOilDbContext>();
    }

    private static EntityErrorLog NewErrorLog(string message, DateTime occurredAtUtc) =>
        new()
        {
            OccurredAtUtc = occurredAtUtc,
            Source = ErrorLogSources.Backend,
            Area = ErrorLogAreas.ApiRequest,
            ExceptionType = typeof(InvalidOperationException).FullName!,
            Message = message
        };
}
