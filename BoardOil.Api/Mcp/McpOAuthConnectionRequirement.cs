using System.Security.Claims;
using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.Auth;
using BoardOil.Api.OAuth;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.OAuth;
using Microsoft.AspNetCore.Authorization;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BoardOil.Api.Mcp;

public sealed class McpOAuthConnectionRequirement : IAuthorizationRequirement;

public sealed class McpOAuthConnectionAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    IDbContextScopeFactory scopeFactory,
    IOAuthConnectionRepository connectionRepository,
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictAuthorizationManager authorizationManager,
    OAuthEndpointUrlResolver urlResolver,
    TimeProvider timeProvider)
    : AuthorizationHandler<McpOAuthConnectionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        McpOAuthConnectionRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null
            || !OAuthResources.IsMcpPath(httpContext.Request.Path))
        {
            return;
        }

        if (httpContext.Request.Path.StartsWithSegments(
                OAuthResources.McpPath,
                StringComparison.OrdinalIgnoreCase)
            && !httpContext.Request.Path.StartsWithSegments(
                OAuthResources.LegacyMcpPath,
                StringComparison.OrdinalIgnoreCase)
            && context.User.Identities.Any(identity => string.Equals(
                identity.AuthenticationType,
                McpAuthenticationSchemes.PatBearer,
                StringComparison.Ordinal)))
        {
            context.Succeed(requirement);
            return;
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            McpOAuthChallengeState.MarkInvalidToken(httpContext);
        }

        var authorizationId = context.User.FindFirst(
            OAuthAuthorizationService.AuthorizationIdClaim)?.Value;
        if (string.IsNullOrWhiteSpace(authorizationId)
            || !string.Equals(
                context.User.GetAuthorizationId(),
                authorizationId,
                StringComparison.Ordinal)
            || !TryGetInt32Claim(
                context.User,
                OAuthAuthorizationService.UserIdClaim,
                out var userId)
            || !TryGetInt32Claim(
                context.User,
                OAuthAuthorizationService.OAuthConnectionIdClaim,
                out var connectionId)
            || !TryGetInt32Claim(
                context.User,
                OAuthAuthorizationService.OAuthConnectionGrantIdClaim,
                out var grantId))
        {
            return;
        }

        var authorization = await authorizationManager.FindByIdAsync(authorizationId);
        if (authorization is null
            || !await authorizationManager.HasStatusAsync(authorization, Statuses.Valid)
            || !string.Equals(
                await authorizationManager.GetSubjectAsync(authorization),
                userId.ToString(),
                StringComparison.Ordinal))
        {
            return;
        }

        var applicationId = await authorizationManager.GetApplicationIdAsync(authorization);
        var application = string.IsNullOrWhiteSpace(applicationId)
            ? null
            : await applicationManager.FindByIdAsync(applicationId);
        if (application is null || !await IsApplicationActiveAsync(application))
        {
            return;
        }

        var applicationClientId = await applicationManager.GetClientIdAsync(application);

        using var scope = scopeFactory.Create();
        var grant = await connectionRepository.GetGrantByAuthorizationIdAsync(authorizationId);
        if (grant is null
            || !string.Equals(grant.OpenIddictApplicationId, applicationId, StringComparison.Ordinal)
            || !string.Equals(grant.OAuthClientId, applicationClientId, StringComparison.Ordinal)
            || grant.Id != grantId
            || grant.OAuthConnectionId != connectionId
            || grant.OAuthConnection.UserId != userId
            || grant.RevokedAtUtc is not null
            || grant.OAuthConnection.ActiveGrantId != grant.Id
            || grant.OAuthConnection.RevokedAtUtc is not null
            || !IsActiveUser(grant.OAuthConnection.User))
        {
            return;
        }

        var resourcePath = OAuthResources.ResolveResourcePath(httpContext.Request.Path);
        var resource = await urlResolver.ResolveAsync(httpContext.Request, resourcePath);
        var audiences = context.User.GetAudiences();
        var tokenScopes = context.User.GetScopes();
        var approvedScopes = grant.ApprovedScopesCsv.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (audiences.Length != 1
            || !string.Equals(audiences[0], resource, StringComparison.Ordinal)
            || !string.Equals(grant.Resource, resource, StringComparison.Ordinal)
            || tokenScopes.Length == 0
            || tokenScopes.Except(approvedScopes, StringComparer.Ordinal).Any()
            || !string.Equals(
                context.User.GetClaim(Claims.Subject),
                userId.ToString(),
                StringComparison.Ordinal))
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var connection = grant.OAuthConnection;
        var shouldUpdateLastUsedAt = !connection.LastUsedAtUtc.HasValue
            || connection.LastUsedAtUtc.Value.Date < now.Date;
        if (shouldUpdateLastUsedAt)
        {
            connection.LastUsedAtUtc = now;
            await scope.SaveChangesAsync(httpContext.RequestAborted);
        }

        McpOAuthChallengeState.Clear(httpContext);
        context.Succeed(requirement);
    }

    private async Task<bool> IsApplicationActiveAsync(object application)
    {
        var properties = await applicationManager.GetPropertiesAsync(application);
        if (!properties.TryGetValue(
                OAuthDynamicClientRegistrationService.DynamicRegistrationProperty,
                out var dynamicRegistration)
            || dynamicRegistration.ValueKind is not JsonValueKind.True)
        {
            return true;
        }

        if (!properties.TryGetValue(
                OAuthDynamicClientRegistrationService.RegistrationExpiresAtProperty,
                out var expiry))
        {
            return true;
        }

        return expiry.ValueKind is JsonValueKind.String
            && expiry.TryGetDateTimeOffset(out var expiresAt)
            && expiresAt > timeProvider.GetUtcNow();
    }

    private static bool IsActiveUser(EntityUser user) =>
        user.IsActive && user.IdentityType == UserIdentityType.User;

    private static bool TryGetInt32Claim(
        ClaimsPrincipal principal,
        string claimType,
        out int value) =>
        int.TryParse(principal.FindFirst(claimType)?.Value, out value);
}
