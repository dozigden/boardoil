using System.Text.Json;
using BoardOil.Abstractions.OAuth;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class OAuthConnectionManagementServiceTests : TestBaseDb
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton(new RecordingOAuthAuthorizationRevoker());
        services.AddSingleton<IOAuthAuthorizationRevoker>(provider =>
            provider.GetRequiredService<RecordingOAuthAuthorizationRevoker>());
    }

    [Fact]
    public async Task GetOwnConnectionsAsync_ShouldReturnOnlyOwnedActiveConnectionsWithoutSecrets()
    {
        // Arrange
        var owner = await AddUserAsync("owner");
        var otherUser = await AddUserAsync("other");
        var ownedConnection = await AddConnectionAsync(owner, "Owned", "owned-authorization", "owned-application");
        var lastUsedAtUtc = DateTime.UtcNow.AddHours(-2);
        ownedConnection.LastUsedAtUtc = lastUsedAtUtc;
        await DbContextForArrange.SaveChangesAsync();
        var replacedGrant = new EntityOAuthConnectionGrant
        {
            OAuthConnectionId = ownedConnection.Id,
            OpenIddictApplicationId = "previous-application-secret",
            OpenIddictAuthorizationId = "previous-authorization-secret",
            OAuthClientId = "previous-public-client-id",
            OAuthClientDisplayName = "Previous Codex",
            Resource = "https://boardoil.example.com/mcp/oauth",
            ApprovedScopesCsv = "mcp:read",
            ApprovedAtUtc = DateTime.UtcNow.AddDays(-1),
            RevokedAtUtc = DateTime.UtcNow.AddHours(-1),
            RevokedByUserName = owner.UserName,
            RevocationReason = "replaced",
        };
        DbContextForArrange.OAuthConnectionGrants.Add(replacedGrant);
        await DbContextForArrange.SaveChangesAsync();
        var revokedConnection = await AddConnectionAsync(owner, "Revoked", "revoked-authorization", "revoked-application");
        revokedConnection.ActiveGrant!.RevokedAtUtc = DateTime.UtcNow;
        revokedConnection.ActiveGrant = null;
        revokedConnection.RevokedAtUtc = DateTime.UtcNow;
        await DbContextForArrange.SaveChangesAsync();
        await AddConnectionAsync(otherUser, "Other", "other-authorization", "other-application");
        var service = ResolveService<IOAuthConnectionManagementService>();

        // Act
        var result = await service.GetOwnConnectionsAsync(owner.Id);

        // Assert
        Assert.True(result.Success);
        var connection = Assert.Single(result.Data!);
        Assert.Equal("Owned", connection.Name);
        Assert.Equal(owner.Id, connection.Owner.Id);
        Assert.Equal(["mcp:read", "mcp:write"], connection.ApprovedScopes);
        Assert.Equal(lastUsedAtUtc, connection.LastUsedAtUtc);
        var json = JsonSerializer.Serialize(connection);
        Assert.DoesNotContain("owned-authorization", json, StringComparison.Ordinal);
        Assert.DoesNotContain("owned-application", json, StringComparison.Ordinal);
        Assert.DoesNotContain("previous-authorization-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("previous-application-secret", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAllConnectionsAsync_ShouldReturnConnectionsAcrossUsers()
    {
        // Arrange
        var firstUser = await AddUserAsync("first");
        var secondUser = await AddUserAsync("second");
        await AddConnectionAsync(firstUser, "First connection", "first-authorization", "first-application");
        await AddConnectionAsync(secondUser, "Second connection", "second-authorization", "second-application");
        var service = ResolveService<IOAuthConnectionManagementService>();

        // Act
        var result = await service.GetAllConnectionsAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count);
        Assert.Collection(
            result.Data,
            first => Assert.Equal("first", first.Owner.UserName),
            second => Assert.Equal("second", second.Owner.UserName));
    }

    [Fact]
    public async Task RevokeOwnConnectionAsync_ShouldDeleteOnlySelectedConnectionAndRevokeAuthorization()
    {
        // Arrange
        var owner = await AddUserAsync("owner");
        var selected = await AddConnectionAsync(owner, "Selected", "selected-authorization", "selected-application");
        var independent = await AddConnectionAsync(owner, "Independent", "independent-authorization", "independent-application");
        var service = ResolveService<IOAuthConnectionManagementService>();

        // Act
        var result = await service.RevokeOwnConnectionAsync(selected.Id, owner.Id);

        // Assert
        Assert.True(result.Success);
        Assert.False(await DbContextForAssert.OAuthConnections.AnyAsync(x => x.Id == selected.Id));
        Assert.False(await DbContextForAssert.OAuthConnectionGrants
            .AnyAsync(x => x.OAuthConnectionId == selected.Id));

        var storedIndependent = await DbContextForAssert.OAuthConnections
            .Include(x => x.Grants)
            .SingleAsync(x => x.Id == independent.Id);
        Assert.NotNull(storedIndependent.ActiveGrantId);
        Assert.Null(storedIndependent.RevokedAtUtc);
        Assert.Null(Assert.Single(storedIndependent.Grants).RevokedAtUtc);

        var revoker = ResolveService<RecordingOAuthAuthorizationRevoker>();
        Assert.Equal(["selected-authorization"], revoker.AuthorizationIds);
    }

    [Fact]
    public async Task RevokeOwnConnectionAsync_WhenConnectionBelongsToAnotherUser_ShouldReturnNotFound()
    {
        // Arrange
        var owner = await AddUserAsync("owner");
        var otherUser = await AddUserAsync("other");
        var connection = await AddConnectionAsync(otherUser, "Other", "other-authorization", "other-application");
        var service = ResolveService<IOAuthConnectionManagementService>();

        // Act
        var result = await service.RevokeOwnConnectionAsync(connection.Id, owner.Id);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.True(await DbContextForAssert.OAuthConnections.AnyAsync(x => x.Id == connection.Id));
        Assert.Empty(ResolveService<RecordingOAuthAuthorizationRevoker>().AuthorizationIds);
    }

    [Fact]
    public async Task RevokeConnectionAsAdminAsync_ShouldRevokeAnotherUsersConnection()
    {
        // Arrange
        var owner = await AddUserAsync("owner");
        var connection = await AddConnectionAsync(owner, "Owned", "owned-authorization", "owned-application");
        var service = ResolveService<IOAuthConnectionManagementService>();

        // Act
        var result = await service.RevokeConnectionAsAdminAsync(connection.Id, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.False(await DbContextForAssert.OAuthConnections.AnyAsync(x => x.Id == connection.Id));
        Assert.False(await DbContextForAssert.OAuthConnectionGrants
            .AnyAsync(x => x.OAuthConnectionId == connection.Id));
        Assert.Equal(["owned-authorization"], ResolveService<RecordingOAuthAuthorizationRevoker>().AuthorizationIds);
    }

    [Fact]
    public async Task RevokeOwnConnectionAsync_WhenAuthorizationRevocationFails_ShouldKeepConnection()
    {
        // Arrange
        var owner = await AddUserAsync("owner");
        var connection = await AddConnectionAsync(owner, "Owned", "owned-authorization", "owned-application");
        var revoker = ResolveService<RecordingOAuthAuthorizationRevoker>();
        revoker.ExceptionToThrow = new InvalidOperationException("Revocation failed.");
        var service = ResolveService<IOAuthConnectionManagementService>();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RevokeOwnConnectionAsync(connection.Id, owner.Id));

        // Assert
        Assert.Equal("Revocation failed.", exception.Message);
        var storedConnection = await DbContextForAssert.OAuthConnections
            .Include(x => x.Grants)
            .SingleAsync(x => x.Id == connection.Id);
        Assert.NotNull(storedConnection.ActiveGrantId);
        Assert.Single(storedConnection.Grants);
    }

    [Fact]
    public async Task RevokeOwnConnectionAsync_WhenPreviouslySoftRevoked_ShouldDeleteConnection()
    {
        // Arrange
        var owner = await AddUserAsync("owner");
        var connection = await AddConnectionAsync(owner, "Owned", "owned-authorization", "owned-application");
        connection.ActiveGrant!.RevokedAtUtc = DateTime.UtcNow.AddMinutes(-1);
        connection.ActiveGrant = null;
        connection.RevokedAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<IOAuthConnectionManagementService>();

        // Act
        var result = await service.RevokeOwnConnectionAsync(connection.Id, owner.Id);

        // Assert
        Assert.True(result.Success);
        Assert.False(await DbContextForAssert.OAuthConnections.AnyAsync(x => x.Id == connection.Id));
        Assert.False(await DbContextForAssert.OAuthConnectionGrants
            .AnyAsync(x => x.OAuthConnectionId == connection.Id));
        Assert.Empty(ResolveService<RecordingOAuthAuthorizationRevoker>().AuthorizationIds);
    }

    private async Task<EntityUser> AddUserAsync(string userName)
    {
        var user = new EntityUser
        {
            UserName = userName,
            DisplayName = $"{userName} display",
            Email = $"{userName}@localhost",
            NormalisedEmail = $"{userName}@localhost",
            PasswordHash = "test-hash",
            Role = UserRole.Standard,
            IdentityType = UserIdentityType.User,
            IsActive = true,
        };
        DbContextForArrange.Users.Add(user);
        await DbContextForArrange.SaveChangesAsync();
        return user;
    }

    private async Task<EntityOAuthConnection> AddConnectionAsync(
        EntityUser owner,
        string name,
        string authorizationId,
        string applicationId)
    {
        var connection = new EntityOAuthConnection
        {
            ResourceType = "mcp",
            Name = name,
            NormalisedName = name.ToUpperInvariant(),
            UserId = owner.Id,
        };
        var grant = new EntityOAuthConnectionGrant
        {
            OAuthConnection = connection,
            OpenIddictApplicationId = applicationId,
            OpenIddictAuthorizationId = authorizationId,
            OAuthClientId = $"{name.ToLowerInvariant()}-public-client-id",
            OAuthClientDisplayName = "Codex",
            Resource = "https://boardoil.example.com/mcp/oauth",
            ApprovedScopesCsv = "mcp:read,mcp:write",
            ApprovedAtUtc = DateTime.UtcNow,
        };
        connection.Grants.Add(grant);
        DbContextForArrange.OAuthConnections.Add(connection);
        await DbContextForArrange.SaveChangesAsync();
        connection.ActiveGrant = grant;
        await DbContextForArrange.SaveChangesAsync();
        return connection;
    }

    private sealed class RecordingOAuthAuthorizationRevoker : IOAuthAuthorizationRevoker
    {
        public List<string> AuthorizationIds { get; } = [];
        public Exception? ExceptionToThrow { get; set; }

        public Task RevokeAsync(string authorizationId, CancellationToken cancellationToken = default)
        {
            AuthorizationIds.Add(authorizationId);
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.CompletedTask;
        }
    }
}
