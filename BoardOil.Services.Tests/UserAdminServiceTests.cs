using BoardOil.Abstractions.Auth;
using BoardOil.Abstractions.Users;
using BoardOil.Contracts.Users;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class UserAdminServiceTests : TestBaseDb
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(TimeProvider.System);
    }

    [Fact]
    public async Task GetUsersAsync_ShouldExcludeClientAccounts()
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
        var service = ResolveService<IUserAdminService>();

        // Act
        var result = await service.GetUsersAsync();

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.DoesNotContain(result.Data!, x => x.UserName == "client-bot");
        Assert.All(result.Data!, x => Assert.Equal(UserIdentityType.User.ToString(), x.IdentityType));
    }

    [Fact]
    public async Task CreateUserAsync_WhenDuplicateEmail_ShouldReturnBadRequest()
    {
        // Arrange
        await RemoveAllUsersAsync();
        await AddUserAsync("member", "member@localhost", "Password1234!");
        var service = ResolveService<IUserAdminService>();

        // Act
        var result = await service.CreateUserAsync(
            new CreateUserRequest("member-two", " MEMBER@LOCALHOST ", "Password1234!", "Standard"));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Email already exists.", result.Message);
    }

    [Fact]
    public async Task UpdateUserAsync_WhenValid_ShouldUpdateEmailRoleAndStatus()
    {
        // Arrange
        await RemoveAllUsersAsync();
        var user = await AddUserAsync("member", "member@localhost", "Password1234!", UserRole.Standard, isActive: true);
        var service = ResolveService<IUserAdminService>();

        // Act
        var result = await service.UpdateUserAsync(
            user.Id,
            new UpdateUserRequest("member-updated@localhost", "Admin", false));

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("member-updated@localhost", result.Data!.Email);
        Assert.Equal(UserRole.Admin.ToString(), result.Data.Role);
        Assert.False(result.Data.IsActive);
    }

    [Fact]
    public async Task ResetUserPasswordAsync_WhenInvalidNewPassword_ShouldReturnBadRequest()
    {
        // Arrange
        await RemoveAllUsersAsync();
        var user = await AddUserAsync("member", "member@localhost", "Password1234!");
        var service = ResolveService<IUserAdminService>();

        // Act
        var result = await service.ResetUserPasswordAsync(user.Id, new ResetUserPasswordRequest("short"));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("newPassword", result.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task ResetUserPasswordAsync_WhenValid_ShouldUpdatePasswordAndRevokeRefreshTokens()
    {
        // Arrange
        await RemoveAllUsersAsync();
        var user = await AddUserAsync("member", "member@localhost", "Password1234!");
        var now = DateTime.UtcNow;
        DbContextForArrange.RefreshTokens.AddRange(
            new EntityRefreshToken
            {
                UserId = user.Id,
                TokenHash = "active-token",
                CreatedAtUtc = now.AddHours(-2),
                ExpiresAtUtc = now.AddDays(2)
            },
            new EntityRefreshToken
            {
                UserId = user.Id,
                TokenHash = "revoked-token",
                CreatedAtUtc = now.AddHours(-3),
                ExpiresAtUtc = now.AddDays(2),
                RevokedAtUtc = now.AddHours(-1)
            });
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<IUserAdminService>();

        // Act
        var result = await service.ResetUserPasswordAsync(user.Id, new ResetUserPasswordRequest("FreshPassword1234!"));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);

        var persistedUser = await DbContextForAssert.Users.SingleAsync(x => x.Id == user.Id);
        var passwordHashService = ResolveService<IPasswordHashService>();
        Assert.True(passwordHashService.VerifyPassword("FreshPassword1234!", persistedUser.PasswordHash));
        Assert.False(passwordHashService.VerifyPassword("Password1234!", persistedUser.PasswordHash));

        var tokens = await DbContextForAssert.RefreshTokens
            .Where(x => x.UserId == user.Id)
            .ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, token => Assert.NotNull(token.RevokedAtUtc));
    }

    private async Task RemoveAllUsersAsync()
    {
        DbContextForArrange.Users.RemoveRange(DbContextForArrange.Users);
        await DbContextForArrange.SaveChangesAsync();
    }

    private async Task<EntityUser> AddUserAsync(
        string userName,
        string email,
        string password,
        UserRole role = UserRole.Admin,
        bool isActive = true,
        UserIdentityType identityType = UserIdentityType.User)
    {
        var passwordHashService = ResolveService<IPasswordHashService>();
        var now = DateTime.UtcNow;
        var user = new EntityUser
        {
            UserName = userName,
            Email = email,
            NormalisedEmail = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHashService.HashPassword(password),
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
