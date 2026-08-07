using System.Security.Claims;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.OAuth;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Mcp;
using Microsoft.AspNetCore.Authorization;
using OpenIddict.Abstractions;
using System.Text.Json;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BoardOil.Api.Mcp;

public sealed class McpOAuthConnectionRequirement : IAuthorizationRequirement;

public sealed class McpOAuthConnectionAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    IDbContextScopeFactory scopeFactory,
    IMcpProjectConnectionRepository connectionRepository,
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
        var publicId = httpContext?.Request.RouteValues["publicId"]?.ToString();
        if (httpContext is null || string.IsNullOrWhiteSpace(publicId))
        {
            return;
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            McpOAuthChallengeState.MarkInvalidToken(httpContext);
        }

        if (!TryGetInt32Claim(
                context.User,
                OAuthAuthorizationService.ClientAccountIdClaim,
                out var clientAccountId)
            || !TryGetInt32Claim(
                context.User,
                OAuthAuthorizationService.ProjectConnectionIdClaim,
                out var connectionId))
        {
            return;
        }

        var authorizationId = context.User.FindFirst(
            OAuthAuthorizationService.AuthorizationIdClaim)?.Value;
        if (string.IsNullOrWhiteSpace(authorizationId))
        {
            return;
        }

        var authorization = await authorizationManager.FindByIdAsync(authorizationId);
        if (authorization is null
            || !await authorizationManager.HasStatusAsync(authorization, Statuses.Valid)
            || !string.Equals(
                await authorizationManager.GetSubjectAsync(authorization),
                clientAccountId.ToString(),
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

        using var scope = scopeFactory.CreateReadOnly();
        var connection = await connectionRepository.GetByPublicIdAsync(publicId);
        if (connection is null
            || connection.Id != connectionId
            || connection.ClientAccountId != clientAccountId
            || connection.RevokedAtUtc is not null
            || !connection.ClientAccount.IsActive
            || connection.ClientAccount.IdentityType != UserIdentityType.Client)
        {
            return;
        }

        var resource = await urlResolver.ResolveAsync(
            httpContext.Request,
            $"/mcp/connections/{connection.PublicId}");
        var audiences = context.User.GetAudiences();
        var tokenScopes = context.User.GetScopes();
        var allowedScopes = connection.AllowedScopesCsv.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (audiences.Length != 1
            || !string.Equals(audiences[0], resource, StringComparison.Ordinal)
            || tokenScopes.Length == 0
            || tokenScopes.Except(allowedScopes, StringComparer.Ordinal).Any()
            || !string.Equals(
                context.User.GetClaim(Claims.Subject),
                clientAccountId.ToString(),
                StringComparison.Ordinal))
        {
            return;
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

        return properties.TryGetValue(
                OAuthDynamicClientRegistrationService.RegistrationExpiresAtProperty,
                out var expiry)
            && expiry.ValueKind is JsonValueKind.String
            && expiry.TryGetDateTimeOffset(out var expiresAt)
            && expiresAt > timeProvider.GetUtcNow();
    }

    private static bool TryGetInt32Claim(
        ClaimsPrincipal principal,
        string claimType,
        out int value) =>
        int.TryParse(principal.FindFirst(claimType)?.Value, out value);
}
