using BoardOil.Contracts.Auth;
using BoardOil.Mcp.Contracts;
using System.Security.Claims;

namespace BoardOil.Api.Mcp;

public sealed class McpAuthorisationService : IMcpAuthorisationService
{
    public PatAccessContext? GetPatAccessContext(ClaimsPrincipal? claimsPrincipal)
    {
        if (claimsPrincipal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var authType = claimsPrincipal.FindFirst("boardoil_auth_type")?.Value;
        if (!string.Equals(authType, "pat", StringComparison.Ordinal))
        {
            return null;
        }

        var scopes = claimsPrincipal
            .FindAll("boardoil_pat_scope")
            .Select(claim => claim.Value)
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .ToHashSet(StringComparer.Ordinal);

        return new PatAccessContext(scopes);
    }

    public McpToolError? EnsurePatToolAccess(PatAccessContext? patAccessContext, string requiredScope, int boardId)
    {
        return EnsurePatScopeAccess(patAccessContext, requiredScope);
    }

    public McpToolError? EnsurePatScopeAccess(PatAccessContext? patAccessContext, string requiredScope)
    {
        if (patAccessContext is null)
        {
            return null;
        }

        if (!patAccessContext.Scopes.Contains(requiredScope))
        {
            return new McpToolError(
                "forbidden",
                $"PAT token requires scope '{requiredScope}' for this tool.",
                403);
        }

        return null;
    }
}

public sealed record PatAccessContext(
    ISet<string> Scopes);
