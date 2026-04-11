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

        var requiredScope = PatApiScopeRules.GetRequiredScope(httpContext.Request);
        if (!HasRequiredPatScope(context.User, requiredScope))
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

    private static bool HasRequiredPatScope(ClaimsPrincipal claimsPrincipal, string requiredScope)
    {
        var scopes = claimsPrincipal
            .FindAll("boardoil_pat_scope")
            .Select(claim => claim.Value)
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .ToHashSet(StringComparer.Ordinal);

        return scopes.Contains(requiredScope);
    }

}
