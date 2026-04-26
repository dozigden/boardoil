using BoardOil.Mcp.Contracts;
using System.Security.Claims;

namespace BoardOil.Api.Mcp;

public interface IMcpAuthorisationService
{
    PatAccessContext? GetPatAccessContext(ClaimsPrincipal? claimsPrincipal);

    McpToolError? EnsurePatScopeAccess(
        PatAccessContext? patAccessContext,
        string requiredScope);

    McpToolError? EnsurePatToolAccess(
        PatAccessContext? patAccessContext,
        string requiredScope,
        int boardId);
}
