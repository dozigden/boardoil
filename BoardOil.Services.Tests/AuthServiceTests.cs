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

    [Fact]
    public async Task GetBootstrapStatusAsync_WhenNoUsers_ShouldRequireInitialAdminSetup()
    {
        // Arrange
        await RemoveAllUsersAsync();
        var service = ResolveService<IAuthService>();

        // Act
        var result = await service.GetBootstrapStatusAsync();

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.RequiresInitialAdminSetup);
    }

    [Fact]
    public async Task GetBootstrapStatusAsync_WhenUsersExist_ShouldNotRequireInitialAdminSetup()
    {
        // Arrange
        var service = ResolveService<IAuthService>();

        // Act
        var result = await service.GetBootstrapStatusAsync();

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.False(result.Data!.RequiresInitialAdminSetup);
    }

    [Fact]
    public async Task LoginAsync_WhenCredentialsValid_ShouldReturnSessionAndPersistRefreshToken()
    {
        // Arrange
        var user = await SeedActiveUserAsync("admin", "Password1234!");
        var service = ResolveService<IAuthService>();

        // Act
        var result = await service.LoginAsync(new LoginRequest(user.UserName, "Password1234!"));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(1, _accessTokenIssuer.CallCount);

        var persistedToken = await DbContextForAssert.RefreshTokens.SingleAsync();
        Assert.Equal(user.Id, persistedToken.UserId);
        Assert.Equal(HashToken(result.Data!.RefreshToken), persistedToken.TokenHash);
        Assert.Null(persistedToken.RevokedAtUtc);
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordInvalid_ShouldReturnUnauthorized()
    {
        // Arrange
        var user = await SeedActiveUserAsync("admin", "Password1234!");
        var service = ResolveService<IAuthService>();

        // Act
        var result = await service.LoginAsync(new LoginRequest(user.UserName, "wrong-password"));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task ChangeOwnPasswordAsync_WhenCurrentPasswordInvalid_ShouldReturnUnauthorized()
    {
        // Arrange
        var user = await SeedActiveUserAsync("admin", "Password1234!");
        var service = ResolveService<IAuthService>();

        // Act
        var result = await service.ChangeOwnPasswordAsync(
            user.Id,
            new ChangeOwnPasswordRequest("wrong-password", "FreshPassword1234!"));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task ChangeOwnPasswordAsync_WhenNewPasswordInvalid_ShouldReturnBadRequest()
    {
        // Arrange
        var user = await SeedActiveUserAsync("admin", "Password1234!");
        var service = ResolveService<IAuthService>();

        // Act
        var result = await service.ChangeOwnPasswordAsync(
            user.Id,
            new ChangeOwnPasswordRequest("Password1234!", "short"));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("newPassword", result.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task ChangeOwnPasswordAsync_WhenValid_ShouldUpdatePasswordAndRevokeActiveRefreshTokens()
    {
        // Arrange
        var user = await SeedActiveUserAsync("admin", "Password1234!");
        var now = DateTime.UtcNow;
        var previouslyRevokedAtUtc = now.AddMinutes(-30);
        DbContextForArrange.RefreshTokens.AddRange(
            new EntityRefreshToken
            {
                UserId = user.Id,
                TokenHash = HashToken("active-token"),
                CreatedAtUtc = now.AddHours(-1),
                ExpiresAtUtc = now.AddDays(1),
                RevokedAtUtc = null
            },
            new EntityRefreshToken
            {
                UserId = user.Id,
                TokenHash = HashToken("already-revoked-token"),
                CreatedAtUtc = now.AddHours(-2),
                ExpiresAtUtc = now.AddDays(1),
                RevokedAtUtc = previouslyRevokedAtUtc
            });
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<IAuthService>();

        // Act
        var result = await service.ChangeOwnPasswordAsync(
            user.Id,
            new ChangeOwnPasswordRequest("Password1234!", "FreshPassword1234!"));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);

        var persistedUser = await DbContextForAssert.Users.SingleAsync(x => x.Id == user.Id);
        var passwordHashService = ResolveService<IPasswordHashService>();
        Assert.True(passwordHashService.VerifyPassword("FreshPassword1234!", persistedUser.PasswordHash));
        Assert.False(passwordHashService.VerifyPassword("Password1234!", persistedUser.PasswordHash));

        var tokens = await DbContextForAssert.RefreshTokens
            .Where(x => x.UserId == user.Id)
            .OrderBy(x => x.TokenHash)
            .ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, token => Assert.NotNull(token.RevokedAtUtc));
        Assert.Contains(tokens, token => token.TokenHash == HashToken("already-revoked-token") && token.RevokedAtUtc == previouslyRevokedAtUtc);
    }

    [Fact]
    public async Task RefreshAsync_WhenTokenMissing_ShouldReturnUnauthorized()
    {
        // Arrange
        var service = ResolveService<IAuthService>();

        // Act
        var result = await service.RefreshAsync(refreshToken: null);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task RefreshAsync_WhenTokenValid_ShouldRotateRefreshToken()
    {
        // Arrange
        var user = await SeedActiveUserAsync("admin", "Password1234!");
        const string oldRefreshToken = "refresh-token-1";
        var oldRefreshTokenHash = HashToken(oldRefreshToken);
        var now = DateTime.UtcNow;
        DbContextForArrange.RefreshTokens.Add(new EntityRefreshToken
        {
            UserId = user.Id,
            TokenHash = oldRefreshTokenHash,
            CreatedAtUtc = now.AddHours(-1),
            ExpiresAtUtc = now.AddDays(1)
        });
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<IAuthService>();

        // Act
        var result = await service.RefreshAsync(oldRefreshToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.NotEqual(oldRefreshToken, result.Data!.RefreshToken);

        var tokens = await DbContextForAssert.RefreshTokens
            .Where(x => x.UserId == user.Id)
            .OrderBy(x => x.Id)
            .ToListAsync();
        Assert.Equal(2, tokens.Count);

        var previousToken = tokens.Single(x => x.TokenHash == oldRefreshTokenHash);
        var replacementToken = tokens.Single(x => x.TokenHash == HashToken(result.Data.RefreshToken));

        Assert.NotNull(previousToken.RevokedAtUtc);
        Assert.Equal(HashToken(result.Data.RefreshToken), previousToken.ReplacedByTokenHash);
        Assert.Null(replacementToken.RevokedAtUtc);
    }

    [Fact]
    public async Task RefreshAsync_WhenTokenInvalid_ShouldReturnUnauthorized()
    {
        // Arrange
        var service = ResolveService<IAuthService>();

        // Act
        var result = await service.RefreshAsync("not-a-real-token");

        // Assert
        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task LogoutAsync_WhenTokenExists_ShouldRevokeToken()
    {
        // Arrange
        var user = await SeedActiveUserAsync("admin", "Password1234!");
        const string refreshToken = "logout-refresh-token";
        var refreshTokenHash = HashToken(refreshToken);
        var now = DateTime.UtcNow;
        DbContextForArrange.RefreshTokens.Add(new EntityRefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            CreatedAtUtc = now.AddHours(-1),
            ExpiresAtUtc = now.AddDays(1)
        });
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<IAuthService>();

        // Act
        var result = await service.LogoutAsync(refreshToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);

        var persisted = await DbContextForAssert.RefreshTokens.SingleAsync(x => x.TokenHash == refreshTokenHash);
        Assert.NotNull(persisted.RevokedAtUtc);
    }

    [Fact]
    public async Task CreateMachinePatAsync_WhenUnsupportedScopeProvided_ShouldReturnBadRequest()
    {
        // Arrange
        var user = await SeedActiveUserAsync("admin", "Password1234!");
        var service = ResolveService<IAuthService>();

        // Act
        var result = await service.CreateMachinePatAsync(
            user.Id,
            new CreateMachinePatRequest("legacy-scope-token", 30, ["mcp"]));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Unsupported scope provided.", result.Message);
    }

    [Fact]
    public async Task CreateMachinePatAsync_WhenValid_ShouldPersistTokenAndReturnPlainTextToken()
    {
        // Arrange
        var user = await SeedActiveUserAsync("admin", "Password1234!");
        var service = ResolveService<IAuthService>();

        // Act
        var result = await service.CreateMachinePatAsync(
            user.Id,
            new CreateMachinePatRequest("agent-token", 30, [MachinePatScopes.McpWrite]));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.StartsWith("bo_pat_", result.Data!.PlainTextToken, StringComparison.Ordinal);
        Assert.Equal("agent-token", result.Data.Token.Name);
        Assert.Equal([MachinePatScopes.McpWrite], result.Data.Token.Scopes);

        var persistedToken = await DbContextForAssert.PersonalAccessTokens.SingleAsync();
        Assert.Equal(user.Id, persistedToken.UserId);
        Assert.Equal(HashToken(result.Data.PlainTextToken), persistedToken.TokenHash);
        Assert.Equal(result.Data.PlainTextToken[..12], persistedToken.TokenPrefix);
        Assert.Equal(MachinePatScopes.McpWrite, persistedToken.ScopesCsv);
        Assert.Null(persistedToken.RevokedAtUtc);
    }

    [Fact]
    public async Task ListMachinePatsAsync_WhenTokensExist_ShouldReturnTokensInCreatedDescendingOrder()
    {
        // Arrange
        var user = await SeedActiveUserAsync("admin", "Password1234!");
        var now = DateTime.UtcNow;
        DbContextForArrange.PersonalAccessTokens.AddRange(
            new EntityPersonalAccessToken
            {
                UserId = user.Id,
                Name = "older-token",
                TokenHash = HashToken("older"),
                TokenPrefix = "bo_pat_OLDER",
                ScopesCsv = MachinePatScopes.McpRead,
                CreatedAtUtc = now.AddMinutes(-5),
                ExpiresAtUtc = now.AddDays(1)
            },
            new EntityPersonalAccessToken
            {
                UserId = user.Id,
                Name = "newer-token",
                TokenHash = HashToken("newer"),
                TokenPrefix = "bo_pat_NEWER",
                ScopesCsv = $"{MachinePatScopes.McpRead},{MachinePatScopes.McpWrite}",
                CreatedAtUtc = now,
                ExpiresAtUtc = now.AddDays(1)
            });
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<IAuthService>();

        // Act
        var result = await service.ListMachinePatsAsync(user.Id);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(["newer-token", "older-token"], result.Data!.Select(x => x.Name).ToArray());
        Assert.Equal([MachinePatScopes.McpRead, MachinePatScopes.McpWrite], result.Data[0].Scopes);
    }

    [Fact]
    public async Task RevokeMachinePatAsync_WhenOwnedAndActive_ShouldMarkTokenRevoked()
    {
        // Arrange
        var user = await SeedActiveUserAsync("admin", "Password1234!");
        var now = DateTime.UtcNow;
        var token = new EntityPersonalAccessToken
        {
            UserId = user.Id,
            Name = "revoke-me",
            TokenHash = HashToken("revoke-me"),
            TokenPrefix = "bo_pat_REVOK",
            ScopesCsv = MachinePatScopes.McpRead,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(1)
        };
        DbContextForArrange.PersonalAccessTokens.Add(token);
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<IAuthService>();

        // Act
        var result = await service.RevokeMachinePatAsync(user.Id, token.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);

        var persisted = await DbContextForAssert.PersonalAccessTokens.SingleAsync(x => x.Id == token.Id);
        Assert.NotNull(persisted.RevokedAtUtc);
    }

    [Fact]
    public async Task RevokeMachinePatAsync_WhenTokenNotOwnedByUser_ShouldReturnNotFound()
    {
        // Arrange
        await RemoveAllUsersAsync();
        var passwordHashService = ResolveService<IPasswordHashService>();
        var now = DateTime.UtcNow;
        var owner = new EntityUser
        {
            UserName = "owner",
            Email = "owner@localhost",
            NormalisedEmail = "owner@localhost",
            PasswordHash = passwordHashService.HashPassword("Password1234!"),
            Role = UserRole.Admin,
            IdentityType = UserIdentityType.User,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var otherUser = new EntityUser
        {
            UserName = "other",
            Email = "other@localhost",
            NormalisedEmail = "other@localhost",
            PasswordHash = passwordHashService.HashPassword("Password1234!"),
            Role = UserRole.Admin,
            IdentityType = UserIdentityType.User,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        DbContextForArrange.Users.AddRange(owner, otherUser);
        await DbContextForArrange.SaveChangesAsync();

        var token = new EntityPersonalAccessToken
        {
            UserId = owner.Id,
            Name = "owner-token",
            TokenHash = HashToken("owner-token"),
            TokenPrefix = "bo_pat_OWNER",
            ScopesCsv = MachinePatScopes.McpRead,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(1)
        };
        DbContextForArrange.PersonalAccessTokens.Add(token);
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<IAuthService>();

        // Act
        var result = await service.RevokeMachinePatAsync(otherUser.Id, token.Id);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    private async Task RemoveAllUsersAsync()
    {
        DbContextForArrange.Users.RemoveRange(DbContextForArrange.Users);
        await DbContextForArrange.SaveChangesAsync();
    }

    private async Task<EntityUser> SeedActiveUserAsync(
        string userName,
        string password,
        UserRole role = UserRole.Admin,
        UserIdentityType identityType = UserIdentityType.User)
    {
        await RemoveAllUsersAsync();
        var now = DateTime.UtcNow;
        var passwordHashService = ResolveService<IPasswordHashService>();
        var user = new EntityUser
        {
            UserName = userName,
            Email = $"{userName}@localhost",
            NormalisedEmail = $"{userName}@localhost",
            PasswordHash = passwordHashService.HashPassword(password),
            Role = role,
            IdentityType = identityType,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        DbContextForArrange.Users.Add(user);
        await DbContextForArrange.SaveChangesAsync();
        return user;
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

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
