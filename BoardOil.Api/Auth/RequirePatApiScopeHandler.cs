using BoardOil.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace BoardOil.Api.Auth;

internal sealed class RequirePatApiScopeHandler(IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<RequirePatApiScopeRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequirePatApiScopeRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        if (!IsPatPrincipal(context.User))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            context.Fail();
            return Task.CompletedTask;
        }

        var path = httpContext.Request.Path;
        if (!path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (path.StartsWithSegments("/api/auth/access-tokens", StringComparison.OrdinalIgnoreCase))
        {
            context.Fail();
            return Task.CompletedTask;
        }

        var requiredScope = GetRequiredScope(httpContext.Request);
        if (!HasRequiredPatScope(context.User, requiredScope))
        {
            context.Fail();
            return Task.CompletedTask;
        }

        if (!HasBoardAllowListAccess(context.User, httpContext))
        {
            context.Fail();
            return Task.CompletedTask;
        }

        context.Succeed(requirement);
        return Task.CompletedTask;
    }

    private static bool IsPatPrincipal(ClaimsPrincipal claimsPrincipal)
    {
        var authType = claimsPrincipal.FindFirst("boardoil_auth_type")?.Value;
        return string.Equals(authType, "pat", StringComparison.Ordinal);
    }

    private static string GetRequiredScope(HttpRequest request)
    {
        if (IsSystemPath(request.Path))
        {
            return MachinePatScopes.ApiSystem;
        }

        if (IsAdminPath(request.Path))
        {
            return MachinePatScopes.ApiAdmin;
        }

        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
        {
            return MachinePatScopes.ApiRead;
        }

        return MachinePatScopes.ApiWrite;
    }

    private static bool IsSystemPath(PathString path) =>
        path.StartsWithSegments("/api/system", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/api/admin/boards", StringComparison.OrdinalIgnoreCase);

    private static bool IsAdminPath(PathString path) =>
        path.StartsWithSegments("/api/admin", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/api/configuration", StringComparison.OrdinalIgnoreCase);

    private static bool HasRequiredPatScope(ClaimsPrincipal claimsPrincipal, string requiredScope)
    {
        var scopes = claimsPrincipal
            .FindAll("boardoil_pat_scope")
            .Select(claim => claim.Value)
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .ToHashSet(StringComparer.Ordinal);

        return scopes.Contains(requiredScope);
    }

    private static bool HasBoardAllowListAccess(ClaimsPrincipal claimsPrincipal, HttpContext httpContext)
    {
        var boardAccessMode = claimsPrincipal.FindFirst("boardoil_pat_board_access_mode")?.Value;
        if (string.IsNullOrWhiteSpace(boardAccessMode)
            || string.Equals(boardAccessMode, MachinePatBoardAccessModes.All, StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(boardAccessMode, MachinePatBoardAccessModes.Selected, StringComparison.Ordinal)
            && IsBoardCreateRequest(httpContext.Request))
        {
            return false;
        }

        var allowedBoardIds = ParseAllowedBoardIds(claimsPrincipal.FindFirst("boardoil_pat_allowed_board_ids")?.Value);
        var routeBoardId = TryResolveBoardId(httpContext);
        if (routeBoardId is null)
        {
            return true;
        }

        return allowedBoardIds.Contains(routeBoardId.Value);
    }

    private static bool IsBoardCreateRequest(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method))
        {
            return false;
        }

        if (!request.Path.StartsWithSegments("/api/boards", StringComparison.OrdinalIgnoreCase, out var remaining))
        {
            return false;
        }

        if (!remaining.HasValue || string.IsNullOrWhiteSpace(remaining.Value))
        {
            return true;
        }

        var trimmed = remaining.Value.Trim('/');
        return trimmed.Equals("import", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("import/tasksmd", StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<int> ParseAllowedBoardIds(string? allowedBoardIdsClaim)
    {
        return (allowedBoardIdsClaim ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(rawBoardId => int.TryParse(rawBoardId, out var boardId) ? (int?)boardId : null)
            .Where(boardId => boardId is > 0)
            .Select(boardId => boardId!.Value)
            .ToHashSet();
    }

    private static int? TryResolveBoardId(HttpContext httpContext)
    {
        if (httpContext.Request.RouteValues.TryGetValue("boardId", out var boardIdValue)
            && int.TryParse(Convert.ToString(boardIdValue), out var parsedBoardId)
            && parsedBoardId > 0)
        {
            return parsedBoardId;
        }

        var segments = httpContext.Request.Path
            .Value?
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? [];
        if (segments.Length >= 3
            && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase)
            && segments[1].Equals("boards", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(segments[2], out parsedBoardId)
            && parsedBoardId > 0)
        {
            return parsedBoardId;
        }

        return null;
    }
}
