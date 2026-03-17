using System.Security.Claims;
using BoardOil.Services.Contracts;

namespace BoardOil.Services.Abstractions;

public interface IAuthService
{
    Task<ApiResult<AuthSessionTokens>> RegisterInitialAdminAsync(RegisterInitialAdminRequest request);
    Task<ApiResult<AuthSessionTokens>> LoginAsync(LoginRequest request);
    Task<ApiResult<AuthSessionTokens>> RefreshAsync(string? refreshToken);
    Task<ApiResult> LogoutAsync(string? refreshToken);
    Task<ApiResult<AuthUserDto>> GetMeAsync(ClaimsPrincipal claimsPrincipal);
    string CreateCsrfToken();
}
