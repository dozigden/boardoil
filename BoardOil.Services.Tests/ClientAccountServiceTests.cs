using BoardOil.Abstractions.Users;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Users;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class ClientAccountServiceTests : TestBaseDb
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(TimeProvider.System);
    }

    [Fact]
    public async Task GetClientAccountsAsync_ShouldReturnOnlyClientAccounts()
    {
        // Arrange
        var now = DateTime.UtcNow;
        DbContextForArrange.Users.Add(new EntityUser
        {
            UserName = "client-bot",
            Email = "client-bot@localhost",
            NormalisedEmail = "client-bot@localhost",
            PasswordHash = "hash",
            Role = UserRole.Standard,
            IdentityType = UserIdentityType.Client,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<IClientAccountService>();

        // Act
        var result = await service.GetClientAccountsAsync();

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(["client-bot"], result.Data!.Select(x => x.UserName).ToArray());
    }

    [Fact]
    public async Task CreateClientAccountAsync_WhenDuplicateEmail_ShouldReturnBadRequest()
    {
        // Arrange
        await RemoveAllUsersAsync();
        await AddUserAsync("client-one", "client-one@localhost", UserIdentityType.Client);
        var service = ResolveService<IClientAccountService>();

        // Act
        var result = await service.CreateClientAccountAsync(
            new CreateClientAccountRequest("client-two", " CLIENT-ONE@LOCALHOST ", "Standard"));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Email already exists.", result.Message);
    }

    [Fact]
    public async Task CreateClientAccountAsync_WhenValid_ShouldCreateClientAndInitialToken()
    {
        // Arrange
        await RemoveAllUsersAsync();
        var service = ResolveService<IClientAccountService>();

        // Act
        var result = await service.CreateClientAccountAsync(
            new CreateClientAccountRequest(
                "client-bot",
                "client-bot@localhost",
                "Standard",
                "Initial token",
                30,
                [MachinePatScopes.ApiRead]));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("client-bot", result.Data!.Account.UserName);
        Assert.Equal("client-bot@localhost", result.Data.Account.Email);
        Assert.Equal(UserRole.Standard.ToString(), result.Data.Account.Role);
        Assert.Equal([MachinePatScopes.ApiRead], result.Data.Token.Token.Scopes);
        Assert.StartsWith("bo_pat_", result.Data.Token.PlainTextToken, StringComparison.Ordinal);

        var persistedUser = await DbContextForAssert.Users.SingleAsync();
        Assert.Equal(UserIdentityType.Client, persistedUser.IdentityType);
        Assert.Equal(result.Data.Account.Id, persistedUser.Id);

        var persistedToken = await DbContextForAssert.PersonalAccessTokens.SingleAsync();
        Assert.Equal(persistedUser.Id, persistedToken.UserId);
        Assert.Equal("Initial token", persistedToken.Name);
    }

    [Fact]
    public async Task UpdateClientAccountAsync_WhenValid_ShouldUpdateEmailRoleAndStatus()
    {
        // Arrange
        await RemoveAllUsersAsync();
        var user = await AddUserAsync("client-bot", "client-bot@localhost", UserIdentityType.Client, UserRole.Standard, true);
        var service = ResolveService<IClientAccountService>();

        // Act
        var result = await service.UpdateClientAccountAsync(
            user.Id,
            new UpdateClientAccountRequest("client-bot-updated@localhost", "Admin", false));

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("client-bot-updated@localhost", result.Data!.Email);
        Assert.Equal(UserRole.Admin.ToString(), result.Data.Role);
        Assert.False(result.Data.IsActive);
    }

    [Fact]
    public async Task ListClientAccessTokensAsync_WhenClientHasTokens_ShouldReturnNewestFirst()
    {
        // Arrange
        await RemoveAllUsersAsync();
        var client = await AddUserAsync("client-bot", "client-bot@localhost", UserIdentityType.Client);
        var olderToken = await AddPersonalAccessTokenAsync(client.Id, "older-token", MachinePatScopes.ApiRead, DateTime.UtcNow.AddMinutes(-2));
        var newerToken = await AddPersonalAccessTokenAsync(client.Id, "newer-token", $"{MachinePatScopes.ApiRead},{MachinePatScopes.ApiWrite}", DateTime.UtcNow);
        var service = ResolveService<IClientAccountService>();

        // Act
        var result = await service.ListClientAccessTokensAsync(client.Id);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(["newer-token", "older-token"], result.Data!.Select(x => x.Name).ToArray());
        Assert.Equal([MachinePatScopes.ApiRead, MachinePatScopes.ApiWrite], result.Data[0].Scopes);
        Assert.Equal(olderToken.Id, result.Data[1].Id);
        Assert.Equal(newerToken.Id, result.Data[0].Id);
    }

    [Fact]
    public async Task CreateClientAccessTokenAsync_WhenUnsupportedScope_ShouldReturnBadRequest()
    {
        // Arrange
        await RemoveAllUsersAsync();
        var client = await AddUserAsync("client-bot", "client-bot@localhost", UserIdentityType.Client);
        var service = ResolveService<IClientAccountService>();

        // Act
        var result = await service.CreateClientAccessTokenAsync(
            client.Id,
            new CreateClientAccessTokenRequest("invalid-scope", 30, ["legacy:scope"]));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Unsupported scope provided.", result.Message);
    }

    [Fact]
    public async Task CreateClientAccessTokenAsync_WhenValid_ShouldPersistTokenAndReturnPlainTextToken()
    {
        // Arrange
        await RemoveAllUsersAsync();
        var client = await AddUserAsync("client-bot", "client-bot@localhost", UserIdentityType.Client);
        var service = ResolveService<IClientAccountService>();

        // Act
        var result = await service.CreateClientAccessTokenAsync(
            client.Id,
            new CreateClientAccessTokenRequest("new-token", 30, [MachinePatScopes.ApiRead]));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("new-token", result.Data!.Token.Name);
        Assert.Equal([MachinePatScopes.ApiRead], result.Data.Token.Scopes);
        Assert.StartsWith("bo_pat_", result.Data.PlainTextToken, StringComparison.Ordinal);

        var persisted = await DbContextForAssert.PersonalAccessTokens.SingleAsync(x => x.UserId == client.Id && x.Name == "new-token");
        Assert.Equal(client.Id, persisted.UserId);
        Assert.Equal(MachinePatScopes.ApiRead, persisted.ScopesCsv);
    }

    [Fact]
    public async Task RevokeClientAccessTokenAsync_WhenOwnedTokenActive_ShouldMarkRevoked()
    {
        // Arrange
        await RemoveAllUsersAsync();
        var client = await AddUserAsync("client-bot", "client-bot@localhost", UserIdentityType.Client);
        var token = await AddPersonalAccessTokenAsync(client.Id, "revoke-me", MachinePatScopes.ApiRead, DateTime.UtcNow);
        var service = ResolveService<IClientAccountService>();

        // Act
        var result = await service.RevokeClientAccessTokenAsync(client.Id, token.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);

        var persisted = await DbContextForAssert.PersonalAccessTokens.SingleAsync(x => x.Id == token.Id);
        Assert.NotNull(persisted.RevokedAtUtc);
    }

    [Fact]
    public async Task DeleteClientAccountAsync_WhenClientExists_ShouldDeleteUser()
    {
        // Arrange
        await RemoveAllUsersAsync();
        var client = await AddUserAsync("client-bot", "client-bot@localhost", UserIdentityType.Client);
        var service = ResolveService<IClientAccountService>();

        // Act
        var result = await service.DeleteClientAccountAsync(client.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(await DbContextForAssert.Users.SingleOrDefaultAsync(x => x.Id == client.Id));
    }

    private async Task RemoveAllUsersAsync()
    {
        DbContextForArrange.Users.RemoveRange(DbContextForArrange.Users);
        await DbContextForArrange.SaveChangesAsync();
    }

    private async Task<EntityUser> AddUserAsync(
        string userName,
        string email,
        UserIdentityType identityType,
        UserRole role = UserRole.Admin,
        bool isActive = true)
    {
        var now = DateTime.UtcNow;
        var user = new EntityUser
        {
            UserName = userName,
            Email = email,
            NormalisedEmail = email.Trim().ToLowerInvariant(),
            PasswordHash = "hash",
            Role = role,
            IdentityType = identityType,
            IsActive = isActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        DbContextForArrange.Users.Add(user);
        await DbContextForArrange.SaveChangesAsync();
        return user;
    }

    private async Task<EntityPersonalAccessToken> AddPersonalAccessTokenAsync(
        int userId,
        string name,
        string scopesCsv,
        DateTime createdAtUtc)
    {
        var token = new EntityPersonalAccessToken
        {
            UserId = userId,
            Name = name,
            TokenHash = $"{name}-hash",
            TokenPrefix = $"bo_pat_{name[..Math.Min(name.Length, 5)].ToUpperInvariant()}",
            ScopesCsv = scopesCsv,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = createdAtUtc.AddDays(7)
        };
        DbContextForArrange.PersonalAccessTokens.Add(token);
        await DbContextForArrange.SaveChangesAsync();
        return token;
    }
}
