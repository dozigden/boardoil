using BoardOil.Contracts.Common;
using BoardOil.Contracts.Mcp;

namespace BoardOil.Abstractions.Mcp;

public interface IMcpProjectConnectionService
{
    Task<ApiResult<IReadOnlyList<McpProjectConnectionDto>>> GetConnectionsAsync();
    Task<ApiResult<McpProjectConnectionDto>> CreateConnectionAsync(
        int actorUserId,
        CreateMcpProjectConnectionRequest request);
    Task<ApiResult> RevokeConnectionAsync(int actorUserId, int connectionId);
}
