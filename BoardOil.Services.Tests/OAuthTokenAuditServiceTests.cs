using BoardOil.Abstractions.OAuth;
using BoardOil.Contracts.OAuth;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class OAuthTokenAuditServiceTests : TestBaseDb
{
    private static readonly DateTime FixedNowUtc = new(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task PurgeExpiredAsync_ShouldDeleteOnlyAuditsOlderThanRetentionCutoff()
    {
        // Arrange
        var cutoffUtc = FixedNowUtc.AddDays(-OAuthTokenAuditRetention.RetentionDays);
        DbContextForArrange.OAuthTokenAudits.AddRange(
            NewAudit("old", cutoffUtc.AddTicks(-1)),
            NewAudit("equal", cutoffUtc),
            NewAudit("new", cutoffUtc.AddTicks(1)));
        await DbContextForArrange.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = ResolveService<IOAuthTokenAuditService>();

        // Act
        var result = await service.PurgeExpiredAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(OAuthTokenAuditRetention.RetentionDays, result.Data!.RetentionDays);
        Assert.Equal(cutoffUtc, result.Data.CutoffUtc);
        Assert.Equal(1, result.Data.DeletedCount);
        var remainingErrorCodes = await DbContextForAssert.OAuthTokenAudits
            .OrderBy(x => x.ErrorCode)
            .Select(x => x.ErrorCode)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["equal", "new"], remainingErrorCodes);
    }

    [Fact]
    public void Model_ShouldIndexOAuthClientAndOccurrenceTime()
    {
        // Arrange
        var entityType = DbContextForAssert.Model.FindEntityType(typeof(EntityOAuthTokenAudit));

        // Act
        var indexes = entityType!.GetIndexes();

        // Assert
        Assert.Contains(
            indexes,
            index => index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(EntityOAuthTokenAudit.OAuthClientId), nameof(EntityOAuthTokenAudit.OccurredAtUtc)]));
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddLogging();
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(FixedNowUtc));
    }

    private static EntityOAuthTokenAudit NewAudit(string errorCode, DateTime occurredAtUtc) =>
        new()
        {
            OccurredAtUtc = occurredAtUtc,
            Outcome = OAuthTokenAuditOutcomes.Rejected,
            GrantType = "refresh_token",
            ErrorCode = errorCode,
            OAuthClientId = "client"
        };

    private sealed class FixedTimeProvider(DateTime nowUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(nowUtc);
    }
}
