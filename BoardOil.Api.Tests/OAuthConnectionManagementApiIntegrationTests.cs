using System.Net;
using System.Net.Http.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.OAuth;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class OAuthConnectionManagementApiIntegrationTests
    : AuthAuthorisationIntegrationTestBase, IClassFixture<DefaultApiFactoryFixture>
{
    public OAuthConnectionManagementApiIntegrationTests(DefaultApiFactoryFixture fixture)
    {
        UseSharedFactory(fixture);
    }

    [Fact]
    public async Task GetOwnConnections_ShouldReturnOnlySignedInUsersConnectionsWithoutSecrets()
    {
        // Arrange
        var adminClient = CreateClient();
        _ = await AuthenticateAsInitialAdminAsync(adminClient);
        var ownerId = await CreateUserAsAdminAsync(adminClient, "owner", "Password1234!", "Standard");
        var otherUserId = await CreateUserAsAdminAsync(adminClient, "other", "Password1234!", "Standard");
        await AddConnectionAsync(ownerId, "Owned", "owned-authorization-secret", "owned-application-secret");
        await AddConnectionAsync(otherUserId, "Other", "other-authorization-secret", "other-application-secret");
        var ownerClient = CreateClient();
        await LoginAsAsync(ownerClient, "owner", "Password1234!");

        // Act
        var response = await ownerClient.GetAsync("/api/oauth-connections");
        var responseText = await response.Content.ReadAsStringAsync();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<IReadOnlyList<OAuthConnectionDto>>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(envelope?.Data);
        var connection = Assert.Single(envelope!.Data!);
        Assert.Equal("Owned", connection.Name);
        Assert.Equal(ownerId, connection.Owner.Id);
        Assert.DoesNotContain("owned-authorization-secret", responseText, StringComparison.Ordinal);
        Assert.DoesNotContain("owned-application-secret", responseText, StringComparison.Ordinal);
        Assert.DoesNotContain("other-authorization-secret", responseText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RevokeOwnConnection_WhenConnectionBelongsToAnotherUser_ShouldReturnNotFound()
    {
        // Arrange
        var adminClient = CreateClient();
        _ = await AuthenticateAsInitialAdminAsync(adminClient);
        _ = await CreateUserAsAdminAsync(adminClient, "owner", "Password1234!", "Standard");
        var otherUserId = await CreateUserAsAdminAsync(adminClient, "other", "Password1234!", "Standard");
        var connectionId = await AddConnectionAsync(otherUserId, "Other", "other-authorization", "other-application");
        var ownerClient = CreateClient();
        await LoginAsAsync(ownerClient, "owner", "Password1234!");

        // Act
        var response = await ownerClient.DeleteAsync($"/api/oauth-connections/{connectionId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(await ConnectionExistsAsync(connectionId));
    }

    [Fact]
    public async Task GetSystemConnections_WhenStandardUser_ShouldReturnForbidden()
    {
        // Arrange
        var adminClient = CreateClient();
        _ = await AuthenticateAsInitialAdminAsync(adminClient);
        _ = await CreateUserAsAdminAsync(adminClient, "member", "Password1234!", "Standard");
        var memberClient = CreateClient();
        await LoginAsAsync(memberClient, "member", "Password1234!");

        // Act
        var response = await memberClient.GetAsync("/api/system/oauth-connections");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RevokeSystemConnection_WhenAdmin_ShouldRevokeSelectedConnectionOnly()
    {
        // Arrange
        var adminClient = CreateClient();
        _ = await AuthenticateAsInitialAdminAsync(adminClient);
        var ownerId = await CreateUserAsAdminAsync(adminClient, "owner", "Password1234!", "Standard");
        var selectedId = await AddConnectionAsync(ownerId, "Selected", "selected-authorization", "selected-application");
        var independentId = await AddConnectionAsync(ownerId, "Independent", "independent-authorization", "independent-application");

        // Act
        var response = await adminClient.DeleteAsync($"/api/system/oauth-connections/{selectedId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(await ConnectionExistsAsync(selectedId));
        Assert.True(await ConnectionExistsAsync(independentId));
    }

    private async Task<int> AddConnectionAsync(
        int ownerUserId,
        string name,
        string authorizationId,
        string applicationId)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory>();
        await using var dbContext = dbContextFactory.CreateDbContext<BoardOilDbContext>();
        var connection = new EntityOAuthConnection
        {
            ResourceType = "mcp",
            Name = name,
            NormalisedName = name.ToUpperInvariant(),
            UserId = ownerUserId,
        };
        var grant = new EntityOAuthConnectionGrant
        {
            OAuthConnection = connection,
            OpenIddictApplicationId = applicationId,
            OpenIddictAuthorizationId = authorizationId,
            OAuthClientId = $"{name.ToLowerInvariant()}-public-client-id",
            OAuthClientDisplayName = "Codex",
            Resource = "http://localhost/mcp/oauth",
            ApprovedScopesCsv = "mcp:read,mcp:write",
            ApprovedAtUtc = DateTime.UtcNow,
        };
        connection.Grants.Add(grant);
        dbContext.OAuthConnections.Add(connection);
        await dbContext.SaveChangesAsync();
        connection.ActiveGrant = grant;
        await dbContext.SaveChangesAsync();
        return connection.Id;
    }

    private async Task<bool> ConnectionExistsAsync(int connectionId)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory>();
        await using var dbContext = dbContextFactory.CreateDbContext<BoardOilDbContext>();
        return await dbContext.OAuthConnections.AnyAsync(x => x.Id == connectionId);
    }

    private sealed record ApiEnvelope<T>(bool Success, T? Data, int StatusCode, string? Message);
}
