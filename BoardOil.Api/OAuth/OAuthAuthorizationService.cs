using System.Collections.Immutable;
using System.Security.Claims;
using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.Auth;
using BoardOil.Contracts.Auth;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Mcp;
using BoardOil.Data.Abstractions.Users;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BoardOil.Api.OAuth;

public sealed class OAuthAuthorizationService(
    IMcpProjectConnectionRepository connectionRepository,
    IUserRepository userRepository,
    IDbContextScopeFactory scopeFactory,
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictAuthorizationManager authorizationManager,
    OAuthEndpointUrlResolver urlResolver,
    TimeProvider timeProvider)
{
    internal const string ClientAccountIdClaim = "boardoil_client_account_id";
    internal const string ProjectConnectionIdClaim = "boardoil_project_connection_id";
    internal const string AuthorizationIdClaim = "boardoil_authorization_id";
    internal const string ApprovedByUserIdClaim = "boardoil_approved_by_user_id";

    internal const string ClientAccountIdProperty = "boardoil:client_account_id";
    internal const string ProjectConnectionIdProperty = "boardoil:project_connection_id";
    internal const string ApprovedByUserIdProperty = "boardoil:approved_by_user_id";
    internal const string ApprovedByUserNameProperty = "boardoil:approved_by_user_name";
    internal const string ApprovedAtProperty = "boardoil:approved_at";
    internal const string ResourceProperty = "boardoil:resource";

    private static readonly string[] SupportedScopes =
    [
        MachinePatScopes.McpRead,
        MachinePatScopes.McpWrite
    ];

    public async Task<OAuthAuthorizationResolution> ResolveAsync(
        OpenIddictRequest request,
        ClaimsPrincipal actor,
        HttpRequest httpRequest,
        CancellationToken cancellationToken = default)
    {
        if (!actor.TryGetUserId(out var actorUserId))
        {
            return OAuthAuthorizationResolution.Rejected(
                Errors.AccessDenied,
                "An active system administrator must approve this connection.");
        }

        using var scope = scopeFactory.CreateReadOnly();
        var approver = userRepository.Get(actorUserId);
        if (approver is null
            || !approver.IsActive
            || approver.IdentityType != UserIdentityType.User
            || approver.Role != UserRole.Admin)
        {
            return OAuthAuthorizationResolution.Rejected(
                Errors.AccessDenied,
                "An active system administrator must approve this connection.");
        }

        var resources = request.GetResources();
        if (resources.Length != 1
            || !TryGetConnectionPublicId(resources[0], out var publicId))
        {
            return OAuthAuthorizationResolution.Rejected(
                Errors.InvalidTarget,
                "Exactly one BoardOil project-connection resource is required.");
        }

        var connection = await connectionRepository.GetByPublicIdAsync(publicId);
        if (connection is null
            || connection.RevokedAtUtc is not null
            || !IsActiveClientAccount(connection.ClientAccount))
        {
            return OAuthAuthorizationResolution.Rejected(
                Errors.InvalidTarget,
                "The requested project connection is not active.");
        }

        var expectedResource = await urlResolver.ResolveAsync(
            httpRequest,
            $"/mcp/connections/{connection.PublicId}");
        if (!string.Equals(resources[0], expectedResource, StringComparison.Ordinal))
        {
            return OAuthAuthorizationResolution.Rejected(
                Errors.InvalidTarget,
                "The requested resource does not match the project connection URL.");
        }

        var requestedScopes = request.GetScopes();
        var allowedScopes = ParseScopes(connection.AllowedScopesCsv);
        if (requestedScopes.Length == 0
            || requestedScopes.Except(SupportedScopes, StringComparer.Ordinal).Any()
            || requestedScopes.Except(allowedScopes, StringComparer.Ordinal).Any())
        {
            return OAuthAuthorizationResolution.Rejected(
                Errors.InvalidScope,
                "The requested scopes are not allowed for this project connection.");
        }

        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            return OAuthAuthorizationResolution.Rejected(
                Errors.InvalidRequest,
                "A registered OAuth client is required.");
        }

        var application = await applicationManager.FindByClientIdAsync(request.ClientId, cancellationToken);
        if (application is null
            || !await IsApplicationActiveAsync(application, cancellationToken))
        {
            return OAuthAuthorizationResolution.Rejected(
                Errors.InvalidClient,
                "The OAuth client registration is no longer active.");
        }

        foreach (var requestedScope in requestedScopes)
        {
            if (!await applicationManager.HasPermissionAsync(
                    application,
                    Permissions.Prefixes.Scope + requestedScope,
                    cancellationToken))
            {
                return OAuthAuthorizationResolution.Rejected(
                    Errors.InvalidScope,
                    "The OAuth client is not registered for every requested scope.");
            }
        }

        var applicationId = await applicationManager.GetIdAsync(application, cancellationToken);
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            throw new InvalidOperationException("The OAuth client has no persistent identifier.");
        }

        var applicationDisplayName = await applicationManager.GetDisplayNameAsync(application, cancellationToken);
        var context = new OAuthAuthorizationContext(
            applicationId,
            request.ClientId,
            string.IsNullOrWhiteSpace(applicationDisplayName) ? request.ClientId : applicationDisplayName,
            approver.Id,
            approver.UserName,
            connection.Id,
            connection.Name,
            connection.ClientAccount.Id,
            connection.ClientAccount.UserName,
            connection.ClientAccount.DisplayName,
            expectedResource,
            requestedScopes.ToArray());
        return OAuthAuthorizationResolution.Accepted(context);
    }

    public async Task<ClaimsPrincipal> CreatePrincipalAsync(
        OAuthAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        var identity = new ClaimsIdentity(
            authenticationType: OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);
        identity.SetClaim(Claims.Subject, context.ClientAccountId.ToString());
        identity.SetClaim(Claims.Name, context.ClientAccountDisplayName);
        identity.SetClaim(ClientAccountIdClaim, context.ClientAccountId.ToString());
        identity.SetClaim(ProjectConnectionIdClaim, context.ProjectConnectionId.ToString());
        identity.SetClaim(ApprovedByUserIdClaim, context.ApprovedByUserId.ToString());

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(context.Scopes);
        principal.SetResources(context.Resource);
        SetAccessTokenDestinations(principal);

        var descriptor = new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = context.ApplicationId,
            Principal = principal,
            Status = Statuses.Valid,
            Subject = context.ClientAccountId.ToString(),
            Type = AuthorizationTypes.Permanent,
        };
        descriptor.Scopes.UnionWith(context.Scopes);
        descriptor.Properties[ClientAccountIdProperty] = JsonSerializer.SerializeToElement(context.ClientAccountId);
        descriptor.Properties[ProjectConnectionIdProperty] = JsonSerializer.SerializeToElement(context.ProjectConnectionId);
        descriptor.Properties[ApprovedByUserIdProperty] = JsonSerializer.SerializeToElement(context.ApprovedByUserId);
        descriptor.Properties[ApprovedByUserNameProperty] = JsonSerializer.SerializeToElement(context.ApprovedByUserName);
        descriptor.Properties[ApprovedAtProperty] = JsonSerializer.SerializeToElement(timeProvider.GetUtcNow());
        descriptor.Properties[ResourceProperty] = JsonSerializer.SerializeToElement(context.Resource);

        var authorization = await authorizationManager.CreateAsync(descriptor, cancellationToken);
        var authorizationId = await authorizationManager.GetIdAsync(authorization, cancellationToken)
            ?? throw new InvalidOperationException("The OAuth authorization has no persistent identifier.");
        principal.SetAuthorizationId(authorizationId);
        principal.SetClaim(AuthorizationIdClaim, authorizationId);
        SetAccessTokenDestinations(principal);
        return principal;
    }

    public async Task<OAuthTokenExchangeResolution> RevalidateAsync(
        ClaimsPrincipal principal,
        OpenIddictRequest request,
        HttpRequest httpRequest,
        CancellationToken cancellationToken = default)
    {
        var authorizationId = principal.GetAuthorizationId();
        if (string.IsNullOrWhiteSpace(authorizationId))
        {
            return OAuthTokenExchangeResolution.Rejected("The token is not bound to a BoardOil authorization.");
        }

        var authorization = await authorizationManager.FindByIdAsync(authorizationId, cancellationToken);
        if (authorization is null
            || !await authorizationManager.HasStatusAsync(authorization, Statuses.Valid, cancellationToken))
        {
            return OAuthTokenExchangeResolution.Rejected("The BoardOil authorization is no longer active.");
        }

        var applicationId = await authorizationManager.GetApplicationIdAsync(
            authorization,
            cancellationToken);
        var application = string.IsNullOrWhiteSpace(applicationId)
            ? null
            : await applicationManager.FindByIdAsync(applicationId, cancellationToken);
        if (application is null
            || !await IsApplicationActiveAsync(application, cancellationToken))
        {
            return OAuthTokenExchangeResolution.Rejected("The OAuth client registration is no longer active.");
        }

        var properties = await authorizationManager.GetPropertiesAsync(authorization, cancellationToken);
        if (!TryGetInt32(properties, ProjectConnectionIdProperty, out var connectionId)
            || !TryGetInt32(properties, ClientAccountIdProperty, out var clientAccountId)
            || !TryGetString(properties, ResourceProperty, out var resource))
        {
            return OAuthTokenExchangeResolution.Rejected("The BoardOil authorization metadata is invalid.");
        }

        using var scope = scopeFactory.CreateReadOnly();
        var connection = await connectionRepository.GetByIdWithClientAccountAsync(connectionId);
        if (connection is null
            || connection.RevokedAtUtc is not null
            || connection.ClientAccountId != clientAccountId
            || !IsActiveClientAccount(connection.ClientAccount))
        {
            return OAuthTokenExchangeResolution.Rejected("The project connection or client account is no longer active.");
        }

        var currentResource = await urlResolver.ResolveAsync(
            httpRequest,
            $"/mcp/connections/{connection.PublicId}");
        if (!string.Equals(resource, currentResource, StringComparison.Ordinal))
        {
            return OAuthTokenExchangeResolution.Rejected(
                "The project connection resource has changed since this authorization was approved.");
        }

        var grantedScopes = principal.GetScopes();
        var requestedScopes = request.GetScopes();
        var effectiveScopes = grantedScopes;
        if (requestedScopes.Length > 0)
        {
            effectiveScopes = requestedScopes;
        }
        var allowedScopes = ParseScopes(connection.AllowedScopesCsv);
        if (effectiveScopes.Length == 0
            || effectiveScopes.Except(grantedScopes, StringComparer.Ordinal).Any()
            || effectiveScopes.Except(allowedScopes, StringComparer.Ordinal).Any())
        {
            return OAuthTokenExchangeResolution.Rejected("The requested token scopes are no longer allowed.");
        }

        principal.SetScopes(effectiveScopes);
        principal.SetResources(currentResource);
        principal.SetClaim(Claims.Subject, clientAccountId.ToString());
        principal.SetClaim(Claims.Name, connection.ClientAccount.DisplayName);
        principal.SetClaim(ClientAccountIdClaim, clientAccountId.ToString());
        principal.SetClaim(ProjectConnectionIdClaim, connectionId.ToString());
        principal.SetClaim(AuthorizationIdClaim, authorizationId);
        SetAccessTokenDestinations(principal);
        return OAuthTokenExchangeResolution.Accepted(principal);
    }

    private static void SetAccessTokenDestinations(ClaimsPrincipal principal) =>
        principal.SetDestinations(static claim => claim.Type switch
        {
            Claims.Subject or Claims.Name or ClientAccountIdClaim or ProjectConnectionIdClaim
                or AuthorizationIdClaim or ApprovedByUserIdClaim => [Destinations.AccessToken],
            _ => [],
        });

    private static bool TryGetConnectionPublicId(string resource, out string publicId)
    {
        publicId = string.Empty;
        if (!Uri.TryCreate(resource, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3
            || !string.Equals(segments[^3], "mcp", StringComparison.Ordinal)
            || !string.Equals(segments[^2], "connections", StringComparison.Ordinal))
        {
            return false;
        }

        var candidate = segments[^1];
        if (candidate.Length != 64)
        {
            return false;
        }

        publicId = candidate;
        return true;
    }

    private async Task<bool> IsApplicationActiveAsync(
        object application,
        CancellationToken cancellationToken)
    {
        var properties = await applicationManager.GetPropertiesAsync(application, cancellationToken);
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

    private static string[] ParseScopes(string scopes) =>
        scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool IsActiveClientAccount(EntityUser account) =>
        account.IsActive && account.IdentityType == UserIdentityType.Client;

    private static bool TryGetInt32(
        ImmutableDictionary<string, JsonElement> properties,
        string name,
        out int value)
    {
        if (properties.TryGetValue(name, out var element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryGetString(
        ImmutableDictionary<string, JsonElement> properties,
        string name,
        out string value)
    {
        if (properties.TryGetValue(name, out var element)
            && element.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(element.GetString()))
        {
            value = element.GetString()!;
            return true;
        }

        value = string.Empty;
        return false;
    }
}

public sealed record OAuthAuthorizationContext(
    string ApplicationId,
    string OAuthClientId,
    string OAuthClientDisplayName,
    int ApprovedByUserId,
    string ApprovedByUserName,
    int ProjectConnectionId,
    string ProjectConnectionName,
    int ClientAccountId,
    string ClientAccountUserName,
    string ClientAccountDisplayName,
    string Resource,
    string[] Scopes);

public sealed record OAuthAuthorizationResolution(
    OAuthAuthorizationContext? Context,
    string? Error,
    string? ErrorDescription)
{
    public bool Success => Context is not null;

    public static OAuthAuthorizationResolution Accepted(OAuthAuthorizationContext context) =>
        new(context, null, null);

    public static OAuthAuthorizationResolution Rejected(string error, string description) =>
        new(null, error, description);
}

public sealed record OAuthTokenExchangeResolution(
    ClaimsPrincipal? Principal,
    string? ErrorDescription)
{
    public bool Success => Principal is not null;

    public static OAuthTokenExchangeResolution Accepted(ClaimsPrincipal principal) =>
        new(principal, null);

    public static OAuthTokenExchangeResolution Rejected(string description) =>
        new(null, description);
}
