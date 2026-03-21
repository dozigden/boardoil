using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BoardOil.Abstractions.Auth;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Entities;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Services.Auth;

public sealed class AuthService(
    IAuthRepository authRepository,
    IPasswordHashService passwordHashService,
    IAccessTokenIssuer accessTokenIssuer,
    AuthSessionOptions sessionOptions,
    TimeProvider timeProvider,
    IDbContextScopeFactory scopeFactory) : IAuthService
{
    public async Task<ApiResult<AuthSessionTokens>> RegisterInitialAdminAsync(RegisterInitialAdminRequest request)
    {
        using var scope = scopeFactory.Create();

        var validation = ValidateCredentials(request.UserName, request.Password);
        if (validation is not null)
        {
            return ApiErrors.BadRequest("Validation failed.", validation);
        }

        if (await authRepository.AnyUsersAsync())
        {
            return new ApiError(409, "Initial admin already exists.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var user = new BoardUser
        {
            UserName = request.UserName.Trim(),
            PasswordHash = passwordHashService.HashPassword(request.Password),
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        AuthSessionTokens? createdSession = null;

        await scope.Transaction(async (transactionScope, transaction) =>
        {
            authRepository.AddUser(user);
            await transactionScope.SaveChangesAsync();

            createdSession = CreateSession(user);
            await transactionScope.SaveChangesAsync();

            await transaction.CommitAsync();
        });

        return createdSession!;
    }

    public async Task<ApiResult<AuthSessionTokens>> LoginAsync(LoginRequest request)
    {
        using var scope = scopeFactory.Create();

        var normalizedUserName = request.UserName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedUserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ApiErrors.Unauthorized("Invalid username or password.");
        }

        var user = await authRepository.GetUserByUserNameAsync(normalizedUserName);
        if (user is null || !user.IsActive || !passwordHashService.VerifyPassword(request.Password, user.PasswordHash))
        {
            return ApiErrors.Unauthorized("Invalid username or password.");
        }

        var session = CreateSession(user);
        await scope.SaveChangesAsync();
        return session;
    }

    public async Task<ApiResult<AuthSessionTokens>> RefreshAsync(string? refreshToken)
    {
        using var scope = scopeFactory.Create();

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return ApiErrors.Unauthorized("Refresh token is missing.");
        }

        var hash = HashRefreshToken(refreshToken);
        var existingToken = await authRepository.GetRefreshTokenWithUserByHashAsync(hash);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (existingToken is null
            || existingToken.RevokedAtUtc is not null
            || existingToken.ExpiresAtUtc <= now
            || !existingToken.User.IsActive)
        {
            return ApiErrors.Unauthorized("Refresh token is invalid or expired.");
        }

        var newSession = CreateSession(existingToken.User);
        existingToken.RevokedAtUtc = now;
        existingToken.ReplacedByTokenHash = HashRefreshToken(newSession.RefreshToken);
        await scope.SaveChangesAsync();

        return newSession;
    }

    public async Task<ApiResult> LogoutAsync(string? refreshToken)
    {
        using var scope = scopeFactory.Create();

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var hash = HashRefreshToken(refreshToken);
            var existingToken = await authRepository.GetRefreshTokenByHashAsync(hash);
            if (existingToken is not null && existingToken.RevokedAtUtc is null)
            {
                existingToken.RevokedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                await scope.SaveChangesAsync();
            }
        }

        return ApiResults.Ok();
    }

    public async Task<ApiResult<AuthUserDto>> GetMeAsync(ClaimsPrincipal claimsPrincipal)
    {
        using var scope = scopeFactory.CreateReadOnly();

        var userIdClaim = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return ApiErrors.Unauthorized("Invalid identity context.");
        }

        var user = await authRepository.GetActiveUserByIdAsync(userId);
        if (user is null)
        {
            return ApiErrors.Unauthorized("User is not active.");
        }

        return user.ToAuthUserDto();
    }

    public async Task<ApiResult<BootstrapStatusDto>> GetBootstrapStatusAsync()
    {
        using var scope = scopeFactory.CreateReadOnly();

        var hasUsers = await authRepository.AnyUsersAsync();
        return new BootstrapStatusDto(!hasUsers);
    }

    public string CreateCsrfToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private AuthSessionTokens CreateSession(BoardUser user)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var accessTokenExpiresAtUtc = now.AddMinutes(sessionOptions.AccessTokenMinutes);
        var refreshTokenExpiresAtUtc = now.AddDays(sessionOptions.RefreshTokenDays);

        var accessToken = accessTokenIssuer.CreateAccessToken(user, now, accessTokenExpiresAtUtc);
        var refreshToken = CreateRefreshToken();
        authRepository.AddRefreshToken(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashRefreshToken(refreshToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshTokenExpiresAtUtc
        });

        return new AuthSessionTokens(
            accessToken,
            accessTokenExpiresAtUtc,
            refreshToken,
            refreshTokenExpiresAtUtc,
            CreateCsrfToken(),
            user.ToAuthUserDto());
    }

    private static string CreateRefreshToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(64));

    private static string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    private static Dictionary<string, string[]>? ValidateCredentials(string userName, string password)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(userName))
        {
            errors["userName"] = ["Username is required."];
        }
        else if (userName.Trim().Length is < 3 or > 64)
        {
            errors["userName"] = ["Username must be between 3 and 64 characters."];
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            errors["password"] = ["Password is required."];
        }
        else if (password.Length < 10)
        {
            errors["password"] = ["Password must be at least 10 characters."];
        }

        return errors.Count == 0 ? null : errors;
    }
}
