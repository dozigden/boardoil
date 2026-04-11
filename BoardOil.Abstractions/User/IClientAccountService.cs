using BoardOil.Contracts.Contracts;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Users;

namespace BoardOil.Abstractions.Users;

public interface IClientAccountService
{
    Task<ApiResult<IReadOnlyList<ClientAccountDto>>> GetClientAccountsAsync();
    Task<ApiResult<CreatedClientAccountDto>> CreateClientAccountAsync(CreateClientAccountRequest request);
    Task<ApiResult<IReadOnlyList<MachinePatDto>>> ListClientAccessTokensAsync(int clientAccountId);
    Task<ApiResult<CreatedMachinePatDto>> CreateClientAccessTokenAsync(int clientAccountId, CreateClientAccessTokenRequest request);
    Task<ApiResult> RevokeClientAccessTokenAsync(int clientAccountId, int tokenId);
}
