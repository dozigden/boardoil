using BoardOil.Mcp.Contracts;
using System.Security.Claims;

namespace BoardOil.Api.Mcp;

public interface IMcpAuthorisationService
{
    McpAccessContext? GetAccessContext(ClaimsPrincipal? claimsPrincipal);

    McpToolError? EnsureScopeAccess(
        McpAccessContext? accessContext,
        string? requiredScope);

    McpToolError? EnsureToolAccess(
        McpAccessContext? accessContext,
        string? requiredScope,
        int boardId);
}
