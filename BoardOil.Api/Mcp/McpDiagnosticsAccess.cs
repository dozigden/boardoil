using BoardOil.Abstractions.Users;
using BoardOil.Contracts.Auth;
using BoardOil.Mcp.Contracts;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Mcp;

public sealed class McpDiagnosticsAccess(IUserService users)
{
    public async Task<McpToolError?> EnsureAccessAsync(McpAccessContext? accessContext)
    {
        // Diagnostics never inherit the development transport's anonymous actor fallback.
        if (accessContext is null || !McpScopeRules.Allows(accessContext.Scopes, MachinePatScopes.McpDiagnosticsRead))
        {
            return Unavailable();
        }

        var identity = await users.GetCurrentIdentityAsync(accessContext.ActorUserId);
        if (!identity.Success || identity.Data?.Role != BoardOilRoles.Admin)
        {
            return Unavailable();
        }

        return null;
    }

    private static McpToolError Unavailable() =>
        new("forbidden", "Diagnostic MCP tools require an active administrator and explicit 'mcp:diagnostics:read' permission.", 403);
}
