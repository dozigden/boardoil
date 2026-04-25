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
}
