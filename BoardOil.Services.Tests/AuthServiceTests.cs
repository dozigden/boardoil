using System.Security.Cryptography;
using System.Text;
using BoardOil.Abstractions.Auth;
using BoardOil.Contracts.Auth;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Services.Auth;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class AuthServiceTests : TestBaseDb
{
    private readonly TestAccessTokenIssuer _accessTokenIssuer = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton(_accessTokenIssuer);
        services.AddSingleton<IAccessTokenIssuer>(provider => provider.GetRequiredService<TestAccessTokenIssuer>());
        services.AddSingleton(new AuthSessionOptions
        {
            AccessTokenMinutes = 15,
            RefreshTokenDays = 14
        });
        services.AddSingleton<TimeProvider>(TimeProvider.System);
    }

    [Fact]
    public async Task RegisterInitialAdminAsync_WhenUserAlreadyExists_ShouldReturnConflict()
    {
        // Arrange
        var service = ResolveService<IAuthService>();

        // Act
        var result = await service.RegisterInitialAdminAsync(
            new RegisterInitialAdminRequest("admin", "admin@localhost", "Password1234!"));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("Initial admin already exists.", result.Message);
        Assert.Equal(1, await DbContextForAssert.Users.CountAsync());
        Assert.Empty(await DbContextForAssert.RefreshTokens.ToListAsync());
    }

    [Fact]
    public async Task RegisterInitialAdminAsync_WhenCredentialsInvalid_ShouldReturnValidationErrors()
    {
        // Arrange
        var service = ResolveService<IAuthService>();

        // Act
        var result = await service.RegisterInitialAdminAsync(
            new RegisterInitialAdminRequest(" ", "invalid-email", "short"));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("userName", result.ValidationErrors!.Keys);
        Assert.Contains("email", result.ValidationErrors.Keys);
        Assert.Contains("password", result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task RegisterInitialAdminAsync_WhenFirstUser_ShouldCreateAdminSessionAndBoardMemberships()
    {
        // Arrange
        await RemoveAllUsersAsync();
        var now = DateTime.UtcNow;
        DbContextForArrange.Boards.AddRange(
            new EntityBoard
            {
                Name = "Board A",
                Description = string.Empty,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new EntityBoard
            {
                Name = "Board B",
                Description = string.Empty,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<IAuthService>();

        // Act
        var result = await service.RegisterInitialAdminAsync(
            new RegisterInitialAdminRequest("  admin  ", " ADMIN@LOCALHOST ", "Password1234!"));

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, _accessTokenIssuer.CallCount);
        Assert.Equal(UserRole.Admin.ToString(), _accessTokenIssuer.LastRole);

        var persistedUser = await DbContextForAssert.Users.SingleAsync();
        Assert.Equal("admin", persistedUser.UserName);
        Assert.Equal("ADMIN@LOCALHOST", persistedUser.Email);
        Assert.Equal("admin@localhost", persistedUser.NormalisedEmail);
        Assert.Equal(UserRole.Admin, persistedUser.Role);
        Assert.Equal(UserIdentityType.User, persistedUser.IdentityType);
        Assert.True(persistedUser.IsActive);
        Assert.NotEqual("Password1234!", persistedUser.PasswordHash);

        var passwordHashService = ResolveService<IPasswordHashService>();
        Assert.True(passwordHashService.VerifyPassword("Password1234!", persistedUser.PasswordHash));

        var persistedRefreshToken = await DbContextForAssert.RefreshTokens.SingleAsync();
        var expectedRefreshTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(result.Data!.RefreshToken)));
        Assert.Equal(persistedUser.Id, persistedRefreshToken.UserId);
        Assert.Equal(expectedRefreshTokenHash, persistedRefreshToken.TokenHash);
        Assert.Null(persistedRefreshToken.RevokedAtUtc);

        var boardMemberships = await DbContextForAssert.BoardMembers
            .Where(x => x.UserId == persistedUser.Id)
            .OrderBy(x => x.BoardId)
            .ToListAsync();
        Assert.Equal(2, boardMemberships.Count);
        Assert.All(boardMemberships, x => Assert.Equal(BoardMemberRole.Owner, x.Role));
    }

    private async Task RemoveAllUsersAsync()
    {
        DbContextForArrange.Users.RemoveRange(DbContextForArrange.Users);
        await DbContextForArrange.SaveChangesAsync();
    }

    private sealed class TestAccessTokenIssuer : IAccessTokenIssuer
    {
        public int CallCount { get; private set; }
        public string LastRole { get; private set; } = string.Empty;

        public string CreateAccessToken(int userId, string userName, string role, DateTime issuedAtUtc, DateTime expiresAtUtc)
        {
            _ = issuedAtUtc;
            _ = expiresAtUtc;
            CallCount++;
            LastRole = role;
            return $"test-access-token-{userId}-{userName}-{role}";
        }
    }
}
