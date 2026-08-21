using System.Net;
using System.Net.Http.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.OAuth;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.OAuth;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class OAuthTokenAuditRetentionIntegrationTests : TestBaseIntegration
{
    [Fact]
    public async Task Purge_ShouldDeleteOnlyAuditsOlderThanRetentionPeriod()
    {
        // Arrange
        var nowUtc = DateTime.UtcNow;
        await AddAuditsAsync(
            NewAudit("old", nowUtc.AddDays(-OAuthTokenAuditRetention.RetentionDays).AddMinutes(-1)),
            NewAudit("kept", nowUtc));

        // Act
        var response = await Client.PostAsync(
            "/api/system/oauth-token-audits:purge",
            content: null,
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<OAuthTokenAuditPurgeResultDto>>(
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result?.Data);
        Assert.Equal(OAuthTokenAuditRetention.RetentionDays, result!.Data!.RetentionDays);
        Assert.Equal(1, result.Data.DeletedCount);

        await using var dbContext = CreateDbContext(Factory.Services);
        var remainingErrorCodes = await dbContext.OAuthTokenAudits
            .Select(x => x.ErrorCode)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["kept"], remainingErrorCodes);
    }

    private async Task AddAuditsAsync(params EntityOAuthTokenAudit[] audits)
    {
        await using var dbContext = CreateDbContext(Factory.Services);
        dbContext.OAuthTokenAudits.AddRange(audits);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    internal static BoardOilDbContext CreateDbContext(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory>();
        return factory.CreateDbContext<BoardOilDbContext>();
    }

    internal static EntityOAuthTokenAudit NewAudit(string errorCode, DateTime occurredAtUtc) =>
        new()
        {
            OccurredAtUtc = occurredAtUtc,
            Outcome = OAuthTokenAuditOutcomes.Rejected,
            GrantType = "refresh_token",
            ErrorCode = errorCode,
            OAuthClientId = "client"
        };
}

public sealed class OAuthTokenAuditStartupCleanupIntegrationTests
{
    [Fact]
    public async Task ApplicationStartup_ShouldPurgeExpiredOAuthTokenAudits()
    {
        // Arrange
        var databasePath = ApiFactoryIntegrationTestBase.BuildDbPath(
            nameof(OAuthTokenAuditStartupCleanupIntegrationTests));
        await using (var initialFactory = new BoardOilApiFactory(databasePath))
        {
            using var client = initialFactory.CreateClient();
            await using var dbContext = OAuthTokenAuditRetentionIntegrationTests.CreateDbContext(
                initialFactory.Services);
            dbContext.OAuthTokenAudits.AddRange(
                OAuthTokenAuditRetentionIntegrationTests.NewAudit(
                    "old",
                    DateTime.UtcNow.AddDays(-OAuthTokenAuditRetention.RetentionDays).AddMinutes(-1)),
                OAuthTokenAuditRetentionIntegrationTests.NewAudit("kept", DateTime.UtcNow));
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        await using var restartedFactory = new BoardOilApiFactory(databasePath);
        using var restartedClient = restartedFactory.CreateClient();
        await using var assertDbContext = OAuthTokenAuditRetentionIntegrationTests.CreateDbContext(
            restartedFactory.Services);
        var remainingErrorCodes = await assertDbContext.OAuthTokenAudits
            .Select(x => x.ErrorCode)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["kept"], remainingErrorCodes);
    }
}
