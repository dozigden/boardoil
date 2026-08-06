using BoardOil.Abstractions.Mcp;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Mcp;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class McpProjectConnectionServiceTests : TestBaseDb
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(TimeProvider.System);
    }

    [Fact]
    public async Task CreateConnectionAsync_WhenValid_ShouldPersistOpaqueClientOwnedConnection()
    {
        // Arrange
        var clientAccount = await AddClientAccountAsync("repository-client");
        var service = ResolveService<IMcpProjectConnectionService>();

        // Act
        var result = await service.CreateConnectionAsync(
            ActorUserId,
            new CreateMcpProjectConnectionRequest(
                clientAccount.Id,
                "  Repository connection  ",
                [MachinePatScopes.McpWrite, MachinePatScopes.McpRead, MachinePatScopes.McpWrite]));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("Repository connection", result.Data!.Name);
        Assert.Equal(clientAccount.Id, result.Data.ClientAccountId);
        Assert.Equal([MachinePatScopes.McpRead, MachinePatScopes.McpWrite], result.Data.AllowedScopes);
        Assert.Matches("^[0-9a-f]{64}$", result.Data.PublicId);
        Assert.Equal($"/mcp/connections/{result.Data.PublicId}", result.Data.ResourceUrl);
        Assert.True(result.Data.IsActive);
        Assert.Equal(ActorUserId, result.Data.CreatedByUserId);
        Assert.Equal("actor", result.Data.CreatedByUserName);

        var persisted = await DbContextForAssert.McpProjectConnections.SingleAsync();
        Assert.Equal(result.Data.PublicId, persisted.PublicId);
        Assert.Equal(clientAccount.Id, persisted.ClientAccountId);
        Assert.Equal("mcp:read,mcp:write", persisted.AllowedScopesCsv);
        Assert.Null(persisted.RevokedAtUtc);
    }

    [Fact]
    public async Task CreateConnectionAsync_WhenScopeUnsupported_ShouldReturnValidationError()
    {
        // Arrange
        var clientAccount = await AddClientAccountAsync("repository-client");
        var service = ResolveService<IMcpProjectConnectionService>();

        // Act
        var result = await service.CreateConnectionAsync(
            ActorUserId,
            new CreateMcpProjectConnectionRequest(clientAccount.Id, "Repository", ["api:system"]));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("allowedScopes", result.ValidationErrors!.Keys);
        Assert.Empty(await DbContextForAssert.McpProjectConnections.ToListAsync());
    }

    [Fact]
    public async Task CreateConnectionAsync_WhenOwnerIsHuman_ShouldReturnNotFound()
    {
        // Arrange
        var service = ResolveService<IMcpProjectConnectionService>();

        // Act
        var result = await service.CreateConnectionAsync(
            ActorUserId,
            new CreateMcpProjectConnectionRequest(ActorUserId, "Repository", [MachinePatScopes.McpRead]));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Client account not found.", result.Message);
        Assert.Empty(await DbContextForAssert.McpProjectConnections.ToListAsync());
    }

    [Fact]
    public async Task CreateConnectionAsync_WhenMultipleCreated_ShouldUseUniquePublicIds()
    {
        // Arrange
        var clientAccount = await AddClientAccountAsync("repository-client");
        var service = ResolveService<IMcpProjectConnectionService>();

        // Act
        var firstResult = await service.CreateConnectionAsync(
            ActorUserId,
            new CreateMcpProjectConnectionRequest(clientAccount.Id, "First", [MachinePatScopes.McpRead]));
        var secondResult = await service.CreateConnectionAsync(
            ActorUserId,
            new CreateMcpProjectConnectionRequest(clientAccount.Id, "Second", [MachinePatScopes.McpRead]));

        // Assert
        Assert.NotNull(firstResult.Data);
        Assert.NotNull(secondResult.Data);
        Assert.NotEqual(firstResult.Data!.PublicId, secondResult.Data!.PublicId);
        Assert.Equal(2, await DbContextForAssert.McpProjectConnections.CountAsync());
    }

    [Fact]
    public async Task RevokeConnectionAsync_WhenActive_ShouldRetainConnectionWithRevocationMetadata()
    {
        // Arrange
        var clientAccount = await AddClientAccountAsync("repository-client");
        var service = ResolveService<IMcpProjectConnectionService>();
        var createResult = await service.CreateConnectionAsync(
            ActorUserId,
            new CreateMcpProjectConnectionRequest(clientAccount.Id, "Repository", [MachinePatScopes.McpRead]));
        Assert.NotNull(createResult.Data);

        // Act
        var result = await service.RevokeConnectionAsync(ActorUserId, createResult.Data!.Id);

        // Assert
        Assert.True(result.Success);
        var persisted = await DbContextForAssert.McpProjectConnections.SingleAsync();
        Assert.Equal(createResult.Data.Id, persisted.Id);
        Assert.NotNull(persisted.RevokedAtUtc);
        Assert.Equal(ActorUserId, persisted.RevokedByUserId);
        Assert.Equal("actor", persisted.RevokedByUserName);
    }

    private async Task<EntityUser> AddClientAccountAsync(string userName)
    {
        var clientAccount = new EntityUser
        {
            UserName = userName,
            DisplayName = "Repository Client",
            Email = $"{userName}@localhost",
            NormalisedEmail = $"{userName}@localhost",
            PasswordHash = null,
            Role = UserRole.Standard,
            IdentityType = UserIdentityType.Client,
            IsActive = true,
        };
        DbContextForArrange.Users.Add(clientAccount);
        await DbContextForArrange.SaveChangesAsync();
        return clientAccount;
    }
}
