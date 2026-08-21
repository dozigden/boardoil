using BoardOil.Abstractions.OAuth;
using BoardOil.Contracts.OAuth;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.OAuth;
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

    [Fact]
    public void Model_ShouldPersistOnlyMinimisedAuditFields()
    {
        // Arrange
        var entityType = DbContextForAssert.Model.FindEntityType(typeof(EntityOAuthTokenAudit));

        // Act
        var propertyNames = entityType!.GetProperties().Select(property => property.Name).ToArray();

        // Assert
        Assert.DoesNotContain("PresentedTokenId", propertyNames);
        Assert.DoesNotContain("Subject", propertyNames);
        Assert.DoesNotContain("CreatedAtUtc", propertyNames);
        Assert.Equal(64, entityType.FindProperty(nameof(EntityOAuthTokenAudit.RequestedScopes))!.GetMaxLength());
        Assert.Equal(512, entityType.FindProperty(nameof(EntityOAuthTokenAudit.ErrorDescription))!.GetMaxLength());
        Assert.Equal(512, entityType.FindProperty(nameof(EntityOAuthTokenAudit.ErrorUri))!.GetMaxLength());
        Assert.Equal(512, entityType.FindProperty(nameof(EntityOAuthTokenAudit.UserAgent))!.GetMaxLength());
    }

    [Fact]
    public async Task RecordAsync_ShouldConstrainAndSanitiseDiagnosticText()
    {
        // Arrange
        var service = ResolveService<IOAuthTokenAuditService>();
        var longValue = $"diagnostic\r\n{new string('x', 600)}";
        var input = new OAuthTokenAuditInput(
            OAuthTokenAuditOutcomes.Rejected,
            "refresh_token",
            ["unknown", "mcp:write", "mcp:read", "mcp:write"],
            "invalid_grant",
            longValue,
            longValue,
            "sha256:presented",
            null,
            null,
            "client",
            "trace",
            longValue);

        // Act
        await service.RecordAsync(input);

        // Assert
        var audit = await DbContextForAssert.OAuthTokenAudits.SingleAsync(
            TestContext.Current.CancellationToken);
        Assert.Equal("mcp:read mcp:write", audit.RequestedScopes);
        Assert.Equal(512, audit.ErrorDescription!.Length);
        Assert.Equal(512, audit.ErrorUri!.Length);
        Assert.Equal(512, audit.UserAgent!.Length);
        Assert.DoesNotContain('\r', audit.ErrorDescription);
        Assert.DoesNotContain('\n', audit.ErrorDescription);
        Assert.DoesNotContain('\r', audit.ErrorUri);
        Assert.DoesNotContain('\n', audit.ErrorUri);
        Assert.DoesNotContain('\r', audit.UserAgent);
        Assert.DoesNotContain('\n', audit.UserAgent);
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

public sealed class OAuthTokenAuditFailureIsolationServiceTests : TestBaseDb
{
    [Fact]
    public async Task RecordAsync_WhenRepositoryThrows_ShouldNotPropagateFailure()
    {
        // Arrange
        var service = ResolveService<IOAuthTokenAuditService>();
        var input = new OAuthTokenAuditInput(
            OAuthTokenAuditOutcomes.Succeeded,
            "authorization_code",
            [],
            null,
            null,
            null,
            "sha256:presented",
            "sha256:issued",
            null,
            "client",
            "trace",
            "agent");

        // Act
        await service.RecordAsync(input);

        // Assert
        Assert.Empty(DbContextForAssert.OAuthTokenAudits);
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.RemoveAll<IOAuthTokenAuditRepository>();
        services.AddScoped<IOAuthTokenAuditRepository, ThrowingOAuthTokenAuditRepository>();
    }

    private sealed class ThrowingOAuthTokenAuditRepository : IOAuthTokenAuditRepository
    {
        public IQueryable<EntityOAuthTokenAudit> Query() => throw new NotSupportedException();

        public EntityOAuthTokenAudit? Get(int id) => throw new NotSupportedException();

        public void Add(EntityOAuthTokenAudit entity) =>
            throw new InvalidOperationException("Simulated OAuth token audit persistence failure.");

        public void AddRange(IEnumerable<EntityOAuthTokenAudit> entities) => throw new NotSupportedException();

        public void Remove(EntityOAuthTokenAudit entity) => throw new NotSupportedException();

        public void RemoveRange(IEnumerable<EntityOAuthTokenAudit> entities) => throw new NotSupportedException();

        public Task<int> CountAsync(OAuthTokenAuditQuery query) => throw new NotSupportedException();

        public Task<IReadOnlyList<EntityOAuthTokenAudit>> ListAsync(
            OAuthTokenAuditQuery query,
            int offset,
            int limit) => throw new NotSupportedException();

        public Task<int> DeleteOlderThanAsync(
            DateTime cutoffUtc,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
