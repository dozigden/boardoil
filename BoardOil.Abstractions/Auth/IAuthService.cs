using System.Security.Claims;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Abstractions.Auth;

public interface IAuthService
{
    Task<ApiResult<AuthSessionTokens>> RegisterInitialAdminAsync(RegisterInitialAdminRequest request);
    Task<ApiResult<AuthSessionTokens>> LoginAsync(LoginRequest request);
    Task<ApiResult> ChangeOwnPasswordAsync(int userId, ChangeOwnPasswordRequest request);
    Task<ApiResult<AuthSessionTokens>> RefreshAsync(string? refreshToken);
    Task<ApiResult> LogoutAsync(string? refreshToken);
    Task<ApiResult<CreatedMachinePatDto>> CreateMachinePatAsync(int userId, CreateMachinePatRequest request);
    Task<ApiResult<IReadOnlyList<MachinePatDto>>> ListMachinePatsAsync(int userId);
    Task<ApiResult> RevokeMachinePatAsync(int userId, int tokenId);
    Task<ApiResult<AuthUserDto>> GetMeAsync(ClaimsPrincipal claimsPrincipal);
    Task<ApiResult<BootstrapStatusDto>> GetBootstrapStatusAsync();
    string CreateCsrfToken();
}
