using System.Security.Claims;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Abstractions.Auth;

public interface IAuthService
{
    Task<ApiResult<AuthSessionTokens>> RegisterInitialAdminAsync(RegisterInitialAdminRequest request);
    Task<ApiResult<AuthSessionTokens>> LoginAsync(LoginRequest request);
    Task<ApiResult<AuthSessionTokens>> RefreshAsync(string? refreshToken);
    Task<ApiResult> LogoutAsync(string? refreshToken);
    Task<ApiResult<AuthUserDto>> GetMeAsync(ClaimsPrincipal claimsPrincipal);
    Task<ApiResult<BootstrapStatusDto>> GetBootstrapStatusAsync();
    string CreateCsrfToken();
}
