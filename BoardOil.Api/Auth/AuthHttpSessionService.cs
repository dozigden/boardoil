using System.Security.Claims;
using BoardOil.Api.Configuration;
using BoardOil.Api.Extensions;
using BoardOil.Services.Abstractions;
using BoardOil.Services.Contracts;

namespace BoardOil.Api.Auth;

public sealed class AuthHttpSessionService(
    IAuthService authService,
    JwtAuthOptions jwtOptions,
    CsrfOptions csrfOptions) : IAuthHttpSessionService
{
    public async Task<IResult> RegisterInitialAdminAsync(RegisterInitialAdminRequest request, HttpResponse response)
    {
        var result = await authService.RegisterInitialAdminAsync(request);
        if (!result.Success || result.Data is null)
        {
            return result.ToHttpResult();
        }

        WriteAuthCookies(response, result.Data.AccessToken, result.Data.AccessTokenExpiresAtUtc, result.Data.RefreshToken, result.Data.RefreshTokenExpiresAtUtc);
        WriteCsrfCookie(response, result.Data.CsrfToken, result.Data.RefreshTokenExpiresAtUtc);
        return ApiResults.Created(result.Data.ToDto()).ToHttpResult();
    }

    public async Task<IResult> LoginAsync(LoginRequest request, HttpResponse response)
    {
        var result = await authService.LoginAsync(request);
        if (!result.Success || result.Data is null)
        {
            return result.ToHttpResult();
        }

        WriteAuthCookies(response, result.Data.AccessToken, result.Data.AccessTokenExpiresAtUtc, result.Data.RefreshToken, result.Data.RefreshTokenExpiresAtUtc);
        WriteCsrfCookie(response, result.Data.CsrfToken, result.Data.RefreshTokenExpiresAtUtc);
        return ApiResults.Ok(result.Data.ToDto()).ToHttpResult();
    }

    public async Task<IResult> RefreshAsync(HttpRequest request, HttpResponse response)
    {
        request.Cookies.TryGetValue(jwtOptions.RefreshTokenCookieName, out var refreshToken);
        var result = await authService.RefreshAsync(refreshToken);
        if (!result.Success || result.Data is null)
        {
            if (result.StatusCode == 401)
            {
                ClearAuthCookies(response);
            }

            return result.ToHttpResult();
        }

        WriteAuthCookies(response, result.Data.AccessToken, result.Data.AccessTokenExpiresAtUtc, result.Data.RefreshToken, result.Data.RefreshTokenExpiresAtUtc);
        WriteCsrfCookie(response, result.Data.CsrfToken, result.Data.RefreshTokenExpiresAtUtc);
        return ApiResults.Ok(result.Data.ToDto()).ToHttpResult();
    }

    public async Task<IResult> LogoutAsync(HttpRequest request, HttpResponse response)
    {
        request.Cookies.TryGetValue(jwtOptions.RefreshTokenCookieName, out var refreshToken);
        await authService.LogoutAsync(refreshToken);

        ClearAuthCookies(response);
        return ApiResults.Ok().ToHttpResult();
    }

    public IResult GetCsrf(HttpRequest request, HttpResponse response)
    {
        if (!request.Cookies.TryGetValue(csrfOptions.CookieName, out var csrfToken)
            || string.IsNullOrWhiteSpace(csrfToken))
        {
            csrfToken = authService.CreateCsrfToken();
            WriteCsrfCookie(response, csrfToken, DateTime.UtcNow.AddDays(1));
        }

        return ApiResults.Ok(new CsrfTokenDto(csrfToken)).ToHttpResult();
    }

    public async Task<IResult> GetMeAsync(ClaimsPrincipal claimsPrincipal)
    {
        var result = await authService.GetMeAsync(claimsPrincipal);
        return result.ToHttpResult();
    }

    private void WriteAuthCookies(
        HttpResponse response,
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

    private void WriteCsrfCookie(HttpResponse response, string csrfToken, DateTime expiresAtUtc)
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

    private void ClearAuthCookies(HttpResponse response)
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
