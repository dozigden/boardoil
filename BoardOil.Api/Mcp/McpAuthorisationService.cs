using BoardOil.Contracts.Auth;
using BoardOil.Abstractions.Attachment;
using BoardOil.Api.OAuth;
using BoardOil.Mcp.Contracts;
using OpenIddict.Abstractions;
using System.Security.Claims;

namespace BoardOil.Api.Mcp;

public sealed class McpAuthorisationService : IMcpAuthorisationService
{
    public McpAccessContext? GetAccessContext(ClaimsPrincipal? claimsPrincipal)
    {
        if (claimsPrincipal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        if (string.Equals(
                claimsPrincipal.FindFirst("boardoil_auth_type")?.Value,
                "pat",
                StringComparison.Ordinal))
        {
            return CreatePatAccessContext(claimsPrincipal);
        }

        return CreateOAuthAccessContext(claimsPrincipal);
    }

    public McpToolError? EnsureToolAccess(McpAccessContext? accessContext, string? requiredScope, int boardId)
    {
        return EnsureScopeAccess(accessContext, requiredScope);
    }

    public McpToolError? EnsureScopeAccess(McpAccessContext? accessContext, string? requiredScope)
    {
        if (accessContext is null || string.IsNullOrWhiteSpace(requiredScope))
        {
            return null;
        }

        if (!accessContext.Scopes.Contains(requiredScope))
        {
            return new McpToolError(
                "forbidden",
                $"{accessContext.AuthenticationType} token requires scope '{requiredScope}' for this tool.",
                403);
        }

        return null;
    }

    private static McpAccessContext? CreatePatAccessContext(ClaimsPrincipal principal)
    {
        if (!int.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorUserId))
        {
            return null;
        }

        var scopes = principal
            .FindAll("boardoil_pat_scope")
            .Select(claim => claim.Value)
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .ToHashSet(StringComparer.Ordinal);
        AttachmentTransferCredential? credential = null;
        if (int.TryParse(principal.FindFirst("boardoil_pat_id")?.Value, out var patId))
        {
            credential = new(patId, null, null);
        }
        return new McpAccessContext(actorUserId, "PAT", scopes, credential);
    }

    private static McpAccessContext? CreateOAuthAccessContext(ClaimsPrincipal principal)
    {
        if (!int.TryParse(
                principal.FindFirst(OAuthAuthorizationService.UserIdClaim)?.Value,
                out var actorUserId))
        {
            return null;
        }

        var scopes = principal.GetScopes().ToHashSet(StringComparer.Ordinal);
        return new McpAccessContext(actorUserId, "OAuth", scopes,
            new(null, principal.GetTokenId(), principal.GetAuthorizationId()));
    }
}

public sealed record McpAccessContext(
    int ActorUserId,
    string AuthenticationType,
    ISet<string> Scopes,
    AttachmentTransferCredential? Credential = null);
