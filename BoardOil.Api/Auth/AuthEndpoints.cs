using BoardOil.Api.Configuration;
using BoardOil.Api.Extensions;
using BoardOil.Ef;
using BoardOil.Ef.Entities;
using BoardOil.Services.Abstractions;
using BoardOil.Services.Auth;
using BoardOil.Services.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace BoardOil.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/register-initial-admin", RegisterInitialAdminAsync);
        app.MapPost("/api/auth/login", LoginAsync);
        app.MapPost("/api/auth/refresh", RefreshAsync);
        app.MapPost("/api/auth/logout", LogoutAsync);
        app.MapGet("/api/auth/csrf", GetCsrfAsync)
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser);
        app.MapGet("/api/auth/me", GetMeAsync)
            .RequireAuthorization(BoardOilPolicies.AuthenticatedUser);
        app.MapGet("/api/users", (IUserAdminService userAdminService) =>
                userAdminService.GetUsersAsync().ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);
        app.MapPost("/api/users", (CreateUserRequest request, IUserAdminService userAdminService) =>
                userAdminService.CreateUserAsync(request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);
        app.MapPatch("/api/users/{id:int}/role", (int id, UpdateUserRoleRequest request, IUserAdminService userAdminService) =>
                userAdminService.UpdateUserRoleAsync(id, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);
        app.MapPatch("/api/users/{id:int}/status", (int id, UpdateUserStatusRequest request, IUserAdminService userAdminService) =>
                userAdminService.UpdateUserStatusAsync(id, request).ToHttpResult())
            .RequireAuthorization(BoardOilPolicies.AdminOnly);

        return app;
    }

    private static async Task<IResult> RegisterInitialAdminAsync(
        RegisterInitialAdminRequest request,
        BoardOilDbContext dbContext,
        IPasswordHashService passwordHashService,
        JwtAuthOptions jwtOptions,
        CsrfOptions csrfOptions,
        TimeProvider timeProvider,
        HttpResponse response)
    {
        var validation = ValidateCredentials(request.UserName, request.Password);
        if (validation is not null)
        {
            return ApiResults.BadRequest<AuthSessionDto>("Validation failed.", validation).ToHttpResult();
        }

        if (await dbContext.Users.AnyAsync())
        {
            return new ApiResult<AuthSessionDto>(false, default, 409, "Initial admin already exists.").ToHttpResult();
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

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var session = await CreateSessionAsync(dbContext, user, jwtOptions, timeProvider);
        WriteAuthCookies(response, jwtOptions, session.AccessToken, session.AccessTokenExpiresAtUtc, session.RefreshToken, session.RefreshTokenExpiresAtUtc);
        WriteCsrfCookie(response, csrfOptions, session.CsrfToken, session.RefreshTokenExpiresAtUtc);
        return ApiResults.Created(session.ToDto()).ToHttpResult();
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        BoardOilDbContext dbContext,
        IPasswordHashService passwordHashService,
        JwtAuthOptions jwtOptions,
        CsrfOptions csrfOptions,
        TimeProvider timeProvider,
        HttpResponse response)
    {
        var normalizedUserName = request.UserName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedUserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ApiResults.Unauthorized<AuthSessionDto>("Invalid username or password.").ToHttpResult();
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.UserName == normalizedUserName);
        if (user is null || !user.IsActive || !passwordHashService.VerifyPassword(request.Password, user.PasswordHash))
        {
            return ApiResults.Unauthorized<AuthSessionDto>("Invalid username or password.").ToHttpResult();
        }

        var session = await CreateSessionAsync(dbContext, user, jwtOptions, timeProvider);
        WriteAuthCookies(response, jwtOptions, session.AccessToken, session.AccessTokenExpiresAtUtc, session.RefreshToken, session.RefreshTokenExpiresAtUtc);
        WriteCsrfCookie(response, csrfOptions, session.CsrfToken, session.RefreshTokenExpiresAtUtc);
        return ApiResults.Ok(session.ToDto()).ToHttpResult();
    }

    private static async Task<IResult> RefreshAsync(
        BoardOilDbContext dbContext,
        JwtAuthOptions jwtOptions,
        CsrfOptions csrfOptions,
        TimeProvider timeProvider,
        HttpRequest request,
        HttpResponse response)
    {
        if (!request.Cookies.TryGetValue(jwtOptions.RefreshTokenCookieName, out var refreshToken)
            || string.IsNullOrWhiteSpace(refreshToken))
        {
            return ApiResults.Unauthorized<AuthSessionDto>("Refresh token is missing.").ToHttpResult();
        }

        var hash = HashRefreshToken(refreshToken);
        var existingToken = await dbContext.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == hash);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (existingToken is null
            || existingToken.RevokedAtUtc is not null
            || existingToken.ExpiresAtUtc <= now
            || !existingToken.User.IsActive)
        {
            ClearAuthCookies(response, jwtOptions, csrfOptions);
            return ApiResults.Unauthorized<AuthSessionDto>("Refresh token is invalid or expired.").ToHttpResult();
        }

        var newSession = await CreateSessionAsync(dbContext, existingToken.User, jwtOptions, timeProvider);
        existingToken.RevokedAtUtc = now;
        existingToken.ReplacedByTokenHash = HashRefreshToken(newSession.RefreshToken);
        await dbContext.SaveChangesAsync();

        WriteAuthCookies(response, jwtOptions, newSession.AccessToken, newSession.AccessTokenExpiresAtUtc, newSession.RefreshToken, newSession.RefreshTokenExpiresAtUtc);
        WriteCsrfCookie(response, csrfOptions, newSession.CsrfToken, newSession.RefreshTokenExpiresAtUtc);
        return ApiResults.Ok(newSession.ToDto()).ToHttpResult();
    }

    private static async Task<IResult> LogoutAsync(
        BoardOilDbContext dbContext,
        JwtAuthOptions jwtOptions,
        CsrfOptions csrfOptions,
        TimeProvider timeProvider,
        HttpRequest request,
        HttpResponse response)
    {
        if (request.Cookies.TryGetValue(jwtOptions.RefreshTokenCookieName, out var refreshToken)
            && !string.IsNullOrWhiteSpace(refreshToken))
        {
            var hash = HashRefreshToken(refreshToken);
            var existingToken = await dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash);
            if (existingToken is not null && existingToken.RevokedAtUtc is null)
            {
                existingToken.RevokedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                await dbContext.SaveChangesAsync();
            }
        }

        ClearAuthCookies(response, jwtOptions, csrfOptions);
        return ApiResults.Ok().ToHttpResult();
    }

    private static IResult GetCsrfAsync(
        CsrfOptions csrfOptions,
        HttpRequest request,
        HttpResponse response)
    {
        if (!request.Cookies.TryGetValue(csrfOptions.CookieName, out var csrfToken)
            || string.IsNullOrWhiteSpace(csrfToken))
        {
            csrfToken = CreateCsrfToken();
            WriteCsrfCookie(response, csrfOptions, csrfToken, DateTime.UtcNow.AddDays(1));
        }

        return ApiResults.Ok(new CsrfTokenDto(csrfToken)).ToHttpResult();
    }

    [Authorize(Policy = BoardOilPolicies.AuthenticatedUser)]
    private static async Task<IResult> GetMeAsync(
        ClaimsPrincipal claimsPrincipal,
        BoardOilDbContext dbContext)
    {
        var userIdClaim = claimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return ApiResults.Unauthorized<AuthUserDto>("Invalid identity context.").ToHttpResult();
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId && x.IsActive);
        if (user is null)
        {
            return ApiResults.Unauthorized<AuthUserDto>("User is not active.").ToHttpResult();
        }

        return ApiResults.Ok(new AuthUserDto(user.Id, user.UserName, user.Role.ToString())).ToHttpResult();
    }

    private static async Task<AuthSession> CreateSessionAsync(
        BoardOilDbContext dbContext,
        BoardUser user,
        JwtAuthOptions jwtOptions,
        TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var accessTokenExpiresAtUtc = now.AddMinutes(jwtOptions.AccessTokenMinutes);
        var refreshTokenExpiresAtUtc = now.AddDays(jwtOptions.RefreshTokenDays);

        var accessToken = CreateAccessToken(user, jwtOptions, now, accessTokenExpiresAtUtc);
        var refreshToken = CreateRefreshToken();
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashRefreshToken(refreshToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshTokenExpiresAtUtc
        });
        await dbContext.SaveChangesAsync();

        return new AuthSession(
            accessToken,
            accessTokenExpiresAtUtc,
            refreshToken,
            refreshTokenExpiresAtUtc,
            CreateCsrfToken(),
            new AuthUserDto(user.Id, user.UserName, user.Role.ToString()));
    }

    private static string CreateAccessToken(BoardUser user, JwtAuthOptions jwtOptions, DateTime issuedAtUtc, DateTime expiresAtUtc)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            ],
            notBefore: issuedAtUtc,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CreateRefreshToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(64));

    private static string CreateCsrfToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

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

    private static void WriteAuthCookies(
        HttpResponse response,
        JwtAuthOptions jwtOptions,
        string accessToken,
        DateTime accessTokenExpiresAtUtc,
        string refreshToken,
        DateTime refreshTokenExpiresAtUtc)
    {
        response.Cookies.Append(
            jwtOptions.AccessTokenCookieName,
            accessToken,
            CreateCookieOptions(accessTokenExpiresAtUtc));
        response.Cookies.Append(
            jwtOptions.RefreshTokenCookieName,
            refreshToken,
            CreateCookieOptions(refreshTokenExpiresAtUtc));
    }

    private static void WriteCsrfCookie(
        HttpResponse response,
        CsrfOptions csrfOptions,
        string csrfToken,
        DateTime expiresAtUtc)
    {
        response.Cookies.Append(
            csrfOptions.CookieName,
            csrfToken,
            new CookieOptions
            {
                HttpOnly = false,
                IsEssential = true,
                SameSite = SameSiteMode.Strict,
                Secure = false,
                Expires = expiresAtUtc,
                Path = "/"
            });
    }

    private static void ClearAuthCookies(HttpResponse response, JwtAuthOptions jwtOptions, CsrfOptions csrfOptions)
    {
        response.Cookies.Delete(jwtOptions.AccessTokenCookieName);
        response.Cookies.Delete(jwtOptions.RefreshTokenCookieName);
        response.Cookies.Delete(csrfOptions.CookieName);
    }

    private static CookieOptions CreateCookieOptions(DateTime expiresAtUtc) =>
        new()
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Strict,
            Secure = false,
            Expires = expiresAtUtc,
            Path = "/"
        };
}

public sealed record RegisterInitialAdminRequest(string UserName, string Password);
public sealed record LoginRequest(string UserName, string Password);
public sealed record AuthUserDto(int Id, string UserName, string Role);
public sealed record AuthSessionDto(AuthUserDto User, DateTime AccessTokenExpiresAtUtc, DateTime RefreshTokenExpiresAtUtc, string CsrfToken);
public sealed record CsrfTokenDto(string CsrfToken);

internal sealed record AuthSession(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    string CsrfToken,
    AuthUserDto User)
{
    public AuthSessionDto ToDto() =>
        new(User, AccessTokenExpiresAtUtc, RefreshTokenExpiresAtUtc, CsrfToken);
}
