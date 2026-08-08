using BoardOil.Contracts.Common;
using BoardOil.Contracts.OAuth;

namespace BoardOil.Abstractions.OAuth;

public interface IOAuthConnectionManagementService
{
    Task<ApiResult<IReadOnlyList<OAuthConnectionDto>>> GetOwnConnectionsAsync(int actorUserId);
    Task<ApiResult<IReadOnlyList<OAuthConnectionDto>>> GetAllConnectionsAsync();
    Task<ApiResult> RevokeOwnConnectionAsync(int connectionId, int actorUserId, CancellationToken cancellationToken = default);
    Task<ApiResult> RevokeConnectionAsAdminAsync(int connectionId, int actorUserId, CancellationToken cancellationToken = default);
}
