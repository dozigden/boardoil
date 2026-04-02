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

        var boardAccessMode = claimsPrincipal.FindFirst("boardoil_pat_board_access_mode")?.Value;
        boardAccessMode = string.IsNullOrWhiteSpace(boardAccessMode)
            ? MachinePatBoardAccessModes.All
            : boardAccessMode.Trim().ToLowerInvariant();

        var allowedBoardIdsClaim = claimsPrincipal.FindFirst("boardoil_pat_allowed_board_ids")?.Value;
        var allowedBoardIds = (allowedBoardIdsClaim ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(rawBoardId => int.TryParse(rawBoardId, out var boardId) ? (int?)boardId : null)
            .Where(boardId => boardId is > 0)
            .Select(boardId => boardId!.Value)
            .ToHashSet();

        return new PatAccessContext(scopes, boardAccessMode, allowedBoardIds);
    }

    public McpToolError? EnsurePatToolAccess(PatAccessContext? patAccessContext, string requiredScope, int boardId)
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

        if (!string.Equals(patAccessContext.BoardAccessMode, MachinePatBoardAccessModes.All, StringComparison.Ordinal)
            && !patAccessContext.AllowedBoardIds.Contains(boardId))
        {
            return new McpToolError(
                "forbidden",
                $"PAT token is not allowed to access board {boardId}.",
                403);
        }

        return null;
    }
}

public sealed record PatAccessContext(
    ISet<string> Scopes,
    string BoardAccessMode,
    ISet<int> AllowedBoardIds);
