using System.Collections.Immutable;
using System.Security.Claims;
using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Api.Auth;
using BoardOil.Contracts.Auth;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.OAuth;
using BoardOil.Data.Abstractions.Users;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BoardOil.Api.OAuth;

public sealed class OAuthAuthorizationService(
    IOAuthConnectionRepository connectionRepository,
    IUserRepository userRepository,
    IDbContextScopeFactory scopeFactory,
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictAuthorizationManager authorizationManager,
    OAuthEndpointUrlResolver urlResolver,
    TimeProvider timeProvider)
{
    internal const string UserIdClaim = "boardoil_user_id";
    internal const string OAuthConnectionIdClaim = "boardoil_oauth_connection_id";
    internal const string OAuthConnectionGrantIdClaim = "boardoil_oauth_connection_grant_id";
    internal const string AuthorizationIdClaim = "boardoil_authorization_id";

    internal const string UserIdProperty = "boardoil:user_id";
    internal const string ApprovedAtProperty = "boardoil:approved_at";
    internal const string ConnectionNameProperty = "boardoil:connection_name";
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
        var user = await ResolveUserAsync(actor);
        if (user is null)
        {
            return OAuthAuthorizationResolution.Rejected(
                Errors.AccessDenied,
                "An active BoardOil user must authorize this connection.");
        }

        var resources = request.GetResources();
        var expectedResource = await ResolveRequestedMcpResourceAsync(resources, httpRequest);
        if (expectedResource is null)
        {
            return OAuthAuthorizationResolution.Rejected(
                Errors.InvalidTarget,
                "The exact BoardOil MCP OAuth resource is required.");
        }

        var requestedScopes = request.GetScopes();
        if (requestedScopes.Length == 0
            || requestedScopes.Except(SupportedScopes, StringComparer.Ordinal).Any())
        {
            return OAuthAuthorizationResolution.Rejected(
                Errors.InvalidScope,
                "Only mcp:read and mcp:write may be requested.");
        }

        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            return OAuthAuthorizationResolution.Rejected(
                Errors.InvalidRequest,
                "A registered OAuth client is required.");
        }

        var application = await applicationManager.FindByClientIdAsync(request.ClientId, cancellationToken);
        if (application is null || !await IsApplicationActiveAsync(application, cancellationToken))
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
        using var scope = scopeFactory.CreateReadOnly();
        var existingConnections = await connectionRepository.Query()
            .Where(x => x.UserId == user.Id && x.ResourceType == OAuthResources.McpType)
            .Select(x => x.Name)
            .ToArrayAsync(cancellationToken);

        var context = new OAuthAuthorizationContext(
            applicationId,
            request.ClientId,
            string.IsNullOrWhiteSpace(applicationDisplayName) ? request.ClientId : applicationDisplayName,
            user.Id,
            user.UserName,
            user.DisplayName,
            expectedResource,
            requestedScopes.ToArray(),
            existingConnections);
        return OAuthAuthorizationResolution.Accepted(context);
    }

    public async Task<OAuthAuthorizationApprovalResolution> ApproveAsync(
        OAuthAuthorizationContext context,
        OAuthAuthorizationApproval approval,
        CancellationToken cancellationToken = default)
    {
        var name = approval.ConnectionName?.Trim() ?? string.Empty;
        var normalisedName = NormaliseName(name);
        var approvedScopes = approval.ApprovedScopes
            .Where(static scope => !string.IsNullOrWhiteSpace(scope))
            .Select(static scope => scope.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var validationError = ValidateApproval(context, name, approvedScopes);
        if (validationError is not null)
        {
            return OAuthAuthorizationApprovalResolution.Rejected(validationError);
        }

        EntityUser user;
        EntityOAuthConnection? existingConnection;
        using (var scope = scopeFactory.CreateReadOnly())
        {
            var persistedUser = userRepository.Get(context.UserId);
            if (persistedUser is null || !IsActiveUser(persistedUser))
            {
                return OAuthAuthorizationApprovalResolution.Rejected(
                    "The signed-in BoardOil user is no longer active.");
            }

            user = persistedUser;
            existingConnection = await connectionRepository.GetByUserResourceAndNameAsync(
                user.Id,
                OAuthResources.McpType,
                normalisedName);
        }

        if (existingConnection is not null && !approval.ReplaceExisting)
        {
            return OAuthAuthorizationApprovalResolution.ReplacementRequired(
                "A connection with this name already exists for this user. Confirm replacement to revoke its previous authorization.");
        }

        var application = await applicationManager.FindByIdAsync(context.ApplicationId, cancellationToken);
        if (application is null || !await IsApplicationActiveAsync(application, cancellationToken))
        {
            return OAuthAuthorizationApprovalResolution.Rejected(
                "The OAuth client registration is no longer active.");
        }

        var principal = CreatePrincipal(user, context.Resource, approvedScopes);
        var registrationExpiry = await PromoteApplicationAsync(application, cancellationToken);
        string? authorizationId = null;
        EntityOAuthConnectionGrant? grant = null;
        string? replacedAuthorizationId = null;
        var approvalCompleted = false;
        try
        {
            var authorization = await CreateAuthorizationAsync(
                context,
                user,
                name,
                approvedScopes,
                principal,
                cancellationToken);
            authorizationId = await authorizationManager.GetIdAsync(authorization, cancellationToken)
                ?? throw new InvalidOperationException("The OAuth authorization has no persistent identifier.");
            principal.SetAuthorizationId(authorizationId);
            principal.SetClaim(AuthorizationIdClaim, authorizationId);

            using var scope = scopeFactory.Create();
            string? bindingError = null;
            var replacementConfirmationRequired = false;
            await scope.Transaction(async (transactionScope, transaction) =>
            {
                var persistedUser = userRepository.Get(user.Id);
                if (persistedUser is null || !IsActiveUser(persistedUser))
                {
                    bindingError = "The signed-in BoardOil user is no longer active.";
                    return;
                }

                var connection = await connectionRepository.GetByUserResourceAndNameAsync(
                    persistedUser.Id,
                    OAuthResources.McpType,
                    normalisedName);
                if (connection is not null && !approval.ReplaceExisting)
                {
                    bindingError = "A connection with this name was created while consent was open. Confirm replacement before continuing.";
                    replacementConfirmationRequired = true;
                    return;
                }

                var now = timeProvider.GetUtcNow().UtcDateTime;
                if (connection is null)
                {
                    connection = new EntityOAuthConnection
                    {
                        ResourceType = OAuthResources.McpType,
                        Name = name,
                        NormalisedName = normalisedName,
                        UserId = persistedUser.Id,
                        User = persistedUser,
                    };
                    connectionRepository.Add(connection);
                }

                var previousGrant = connection.ActiveGrant;
                grant = new EntityOAuthConnectionGrant
                {
                    OAuthConnection = connection,
                    OpenIddictApplicationId = context.ApplicationId,
                    OpenIddictAuthorizationId = authorizationId,
                    OAuthClientId = context.OAuthClientId,
                    OAuthClientDisplayName = context.OAuthClientDisplayName,
                    Resource = context.Resource,
                    ApprovedScopesCsv = string.Join(',', approvedScopes),
                    ApprovedAtUtc = now,
                };
                connection.Grants.Add(grant);

                // Persist the two new integer keys before assigning the optional active-grant FK.
                await transactionScope.SaveChangesAsync(cancellationToken);

                if (previousGrant is not null)
                {
                    previousGrant.RevokedAtUtc = now;
                    previousGrant.RevokedByUserId = context.UserId;
                    previousGrant.RevokedByUserName = context.UserName;
                    previousGrant.RevocationReason = "replaced";
                    replacedAuthorizationId = previousGrant.OpenIddictAuthorizationId;
                }

                connection.ActiveGrant = grant;
                connection.RevokedAtUtc = null;
                connection.RevokedByUserId = null;
                connection.RevokedByUserName = null;

                await transactionScope.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync();
            });

            if (bindingError is not null || grant is null)
            {
                await RevokeAuthorizationAsync(authorizationId, cancellationToken);
                if (replacementConfirmationRequired)
                {
                    return OAuthAuthorizationApprovalResolution.ReplacementRequired(bindingError!);
                }

                return OAuthAuthorizationApprovalResolution.Rejected(
                    bindingError ?? "The OAuth connection could not be activated.");
            }

            if (!string.IsNullOrWhiteSpace(replacedAuthorizationId))
            {
                await RevokeAuthorizationAsync(replacedAuthorizationId, cancellationToken);
            }

            principal.SetClaim(OAuthConnectionIdClaim, grant.OAuthConnectionId.ToString());
            principal.SetClaim(OAuthConnectionGrantIdClaim, grant.Id.ToString());
            SetAccessTokenDestinations(principal);
            approvalCompleted = true;
            return OAuthAuthorizationApprovalResolution.Accepted(principal);
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(authorizationId))
            {
                await RevokeAuthorizationAsync(authorizationId, cancellationToken);
            }

            throw;
        }
        finally
        {
            if (!approvalCompleted && registrationExpiry is not null)
            {
                using var scope = scopeFactory.CreateReadOnly();
                if (!await connectionRepository.AnyGrantForApplicationAsync(context.ApplicationId))
                {
                    await RestoreApplicationExpiryAsync(
                        application,
                        registrationExpiry.Value,
                        cancellationToken);
                }
            }
        }
    }

    public async Task<OAuthTokenExchangeResolution> RevalidateAsync(
        ClaimsPrincipal principal,
        OpenIddictRequest request,
        HttpRequest httpRequest,
        CancellationToken cancellationToken = default)
    {
        var resources = request.GetResources();
        var currentResource = await ResolveRequestedMcpResourceAsync(resources, httpRequest);
        if (currentResource is null)
        {
            return OAuthTokenExchangeResolution.Rejected(
                Errors.InvalidTarget,
                "The exact BoardOil MCP OAuth resource is required.");
        }

        var authorizationId = principal.GetAuthorizationId();
        if (string.IsNullOrWhiteSpace(authorizationId))
        {
            return OAuthTokenExchangeResolution.Rejected(
                Errors.InvalidGrant,
                "The token is not bound to a BoardOil authorization.");
        }

        var authorization = await authorizationManager.FindByIdAsync(authorizationId, cancellationToken);
        if (authorization is null
            || !await authorizationManager.HasStatusAsync(authorization, Statuses.Valid, cancellationToken))
        {
            return OAuthTokenExchangeResolution.Rejected(
                Errors.InvalidGrant,
                "The BoardOil authorization is no longer active.");
        }

        var authorizationSubject = await authorizationManager.GetSubjectAsync(
            authorization,
            cancellationToken);
        var applicationId = await authorizationManager.GetApplicationIdAsync(authorization, cancellationToken);
        var application = string.IsNullOrWhiteSpace(applicationId)
            ? null
            : await applicationManager.FindByIdAsync(applicationId, cancellationToken);
        if (application is null || !await IsApplicationActiveAsync(application, cancellationToken))
        {
            return OAuthTokenExchangeResolution.Rejected(
                Errors.InvalidGrant,
                "The OAuth client registration is no longer active.");
        }

        var applicationClientId = await applicationManager.GetClientIdAsync(application, cancellationToken);

        using var scope = scopeFactory.CreateReadOnly();
        var grant = await connectionRepository.GetGrantByAuthorizationIdAsync(authorizationId);
        if (grant is null
            || !string.Equals(grant.OpenIddictApplicationId, applicationId, StringComparison.Ordinal)
            || !string.Equals(grant.OAuthClientId, applicationClientId, StringComparison.Ordinal)
            || grant.RevokedAtUtc is not null
            || grant.OAuthConnection.ActiveGrantId != grant.Id
            || grant.OAuthConnection.RevokedAtUtc is not null
            || !string.Equals(
                authorizationSubject,
                grant.OAuthConnection.UserId.ToString(),
                StringComparison.Ordinal)
            || !string.Equals(
                principal.GetClaim(UserIdClaim),
                grant.OAuthConnection.UserId.ToString(),
                StringComparison.Ordinal)
            || !IsActiveUser(grant.OAuthConnection.User))
        {
            return OAuthTokenExchangeResolution.Rejected(
                Errors.InvalidGrant,
                "The OAuth connection or authorization is no longer active.");
        }

        if (!ResourceUrisMatch(grant.Resource, currentResource))
        {
            return OAuthTokenExchangeResolution.Rejected(
                Errors.InvalidGrant,
                "The OAuth resource has changed since this authorization was approved.");
        }

        var grantedScopes = principal.GetScopes();
        var requestedScopes = request.GetScopes();
        var effectiveScopes = requestedScopes.Length > 0 ? requestedScopes : grantedScopes;
        var approvedScopes = ParseScopes(grant.ApprovedScopesCsv);
        if (effectiveScopes.Length == 0
            || effectiveScopes.Except(grantedScopes, StringComparer.Ordinal).Any()
            || effectiveScopes.Except(approvedScopes, StringComparer.Ordinal).Any())
        {
            return OAuthTokenExchangeResolution.Rejected(
                Errors.InvalidGrant,
                "The requested token scopes are no longer allowed.");
        }

        var user = grant.OAuthConnection.User;
        principal.SetScopes(effectiveScopes);
        principal.SetResources(currentResource);
        principal.SetClaim(Claims.Subject, user.Id.ToString());
        principal.SetClaim(Claims.Name, user.DisplayName);
        principal.SetClaim(UserIdClaim, user.Id.ToString());
        principal.SetClaim(OAuthConnectionIdClaim, grant.OAuthConnectionId.ToString());
        principal.SetClaim(OAuthConnectionGrantIdClaim, grant.Id.ToString());
        principal.SetClaim(AuthorizationIdClaim, authorizationId);
        SetAccessTokenDestinations(principal);
        return OAuthTokenExchangeResolution.Accepted(principal);
    }

    private async Task<EntityUser?> ResolveUserAsync(ClaimsPrincipal actor)
    {
        if (!actor.TryGetUserId(out var actorUserId))
        {
            return null;
        }

        using var scope = scopeFactory.CreateReadOnly();
        var user = userRepository.Get(actorUserId);
        if (user is null || !IsActiveUser(user))
        {
            return null;
        }

        return user;
    }

    private async Task<object> CreateAuthorizationAsync(
        OAuthAuthorizationContext context,
        EntityUser user,
        string connectionName,
        string[] approvedScopes,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var descriptor = new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = context.ApplicationId,
            Principal = principal,
            Status = Statuses.Valid,
            Subject = user.Id.ToString(),
            Type = AuthorizationTypes.Permanent,
        };
        descriptor.Scopes.UnionWith(approvedScopes);
        descriptor.Properties[UserIdProperty] = JsonSerializer.SerializeToElement(user.Id);
        descriptor.Properties[ApprovedAtProperty] = JsonSerializer.SerializeToElement(timeProvider.GetUtcNow());
        descriptor.Properties[ConnectionNameProperty] = JsonSerializer.SerializeToElement(connectionName);
        descriptor.Properties[ResourceProperty] = JsonSerializer.SerializeToElement(context.Resource);
        return await authorizationManager.CreateAsync(descriptor, cancellationToken);
    }

    private static ClaimsPrincipal CreatePrincipal(
        EntityUser user,
        string resource,
        string[] approvedScopes)
    {
        var identity = new ClaimsIdentity(
            authenticationType: OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);
        identity.SetClaim(Claims.Subject, user.Id.ToString());
        identity.SetClaim(Claims.Name, user.DisplayName);
        identity.SetClaim(UserIdClaim, user.Id.ToString());

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(approvedScopes);
        principal.SetResources(resource);
        SetAccessTokenDestinations(principal);
        return principal;
    }

    private async Task<JsonElement?> PromoteApplicationAsync(
        object application,
        CancellationToken cancellationToken)
    {
        var properties = await applicationManager.GetPropertiesAsync(application, cancellationToken);
        if (!properties.TryGetValue(
                OAuthDynamicClientRegistrationService.RegistrationExpiresAtProperty,
                out var registrationExpiry))
        {
            return null;
        }

        var descriptor = new OpenIddictApplicationDescriptor();
        await applicationManager.PopulateAsync(descriptor, application, cancellationToken);
        descriptor.Properties.Remove(OAuthDynamicClientRegistrationService.RegistrationExpiresAtProperty);
        await applicationManager.UpdateAsync(application, descriptor, cancellationToken);
        return registrationExpiry.Clone();
    }

    private async Task RestoreApplicationExpiryAsync(
        object application,
        JsonElement registrationExpiry,
        CancellationToken cancellationToken)
    {
        var descriptor = new OpenIddictApplicationDescriptor();
        await applicationManager.PopulateAsync(descriptor, application, cancellationToken);
        descriptor.Properties[OAuthDynamicClientRegistrationService.RegistrationExpiresAtProperty] =
            registrationExpiry;
        await applicationManager.UpdateAsync(application, descriptor, cancellationToken);
    }

    private async Task RevokeAuthorizationAsync(string authorizationId, CancellationToken cancellationToken)
    {
        var authorization = await authorizationManager.FindByIdAsync(authorizationId, cancellationToken);
        if (authorization is not null)
        {
            await authorizationManager.TryRevokeAsync(authorization, cancellationToken);
        }
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

    private static string? ValidateApproval(
        OAuthAuthorizationContext context,
        string name,
        IReadOnlyList<string> approvedScopes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Connection name is required.";
        }

        if (name.Length > 120)
        {
            return "Connection name must be 120 characters or fewer.";
        }

        if (approvedScopes.Count == 0)
        {
            return "Select at least one requested MCP scope.";
        }

        if (approvedScopes.Except(context.RequestedScopes, StringComparer.Ordinal).Any()
            || approvedScopes.Except(SupportedScopes, StringComparer.Ordinal).Any())
        {
            return "Approved scopes must be a subset of the requested MCP scopes.";
        }

        return null;
    }

    private static void SetAccessTokenDestinations(ClaimsPrincipal principal) =>
        principal.SetDestinations(static claim => claim.Type switch
        {
            Claims.Subject or Claims.Name or UserIdClaim or OAuthConnectionIdClaim
                or OAuthConnectionGrantIdClaim or AuthorizationIdClaim
                => [Destinations.AccessToken],
            _ => [],
        });

    private static string NormaliseName(string name) => name.ToUpperInvariant();

    private static string[] ParseScopes(string scopes) =>
        scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private async Task<string?> ResolveRequestedMcpResourceAsync(
        IReadOnlyList<string> requestedResources,
        HttpRequest request)
    {
        if (requestedResources.Count != 1)
        {
            return null;
        }

        var canonicalResource = await urlResolver.ResolveAsync(request, OAuthResources.McpPath);
        if (ResourceUrisMatch(requestedResources[0], canonicalResource))
        {
            return canonicalResource;
        }

        var legacyResource = await urlResolver.ResolveAsync(request, OAuthResources.LegacyMcpPath);
        return ResourceUrisMatch(requestedResources[0], legacyResource)
            ? legacyResource
            : null;
    }

    private static bool ResourceUrisMatch(string value, string expected)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var valueUri)
            || !Uri.TryCreate(expected, UriKind.Absolute, out var expectedUri))
        {
            return false;
        }

        return string.Equals(valueUri.Scheme, expectedUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(valueUri.Host, expectedUri.Host, StringComparison.OrdinalIgnoreCase)
            && valueUri.Port == expectedUri.Port
            && string.Equals(valueUri.UserInfo, expectedUri.UserInfo, StringComparison.Ordinal)
            && string.Equals(valueUri.AbsolutePath, expectedUri.AbsolutePath, StringComparison.Ordinal)
            && string.Equals(valueUri.Query, expectedUri.Query, StringComparison.Ordinal)
            && string.Equals(valueUri.Fragment, expectedUri.Fragment, StringComparison.Ordinal);
    }

    private static bool IsActiveUser(EntityUser user) =>
        user.IsActive && user.IdentityType == UserIdentityType.User;
}

public sealed record OAuthAuthorizationContext(
    string ApplicationId,
    string OAuthClientId,
    string OAuthClientDisplayName,
    int UserId,
    string UserName,
    string UserDisplayName,
    string Resource,
    string[] RequestedScopes,
    IReadOnlyList<string> ExistingConnections);

public sealed record OAuthAuthorizationApproval(
    string? ConnectionName,
    string[] ApprovedScopes,
    bool ReplaceExisting);

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

public sealed record OAuthAuthorizationApprovalResolution(
    ClaimsPrincipal? Principal,
    string? ErrorDescription,
    bool RequiresReplacementConfirmation)
{
    public bool Success => Principal is not null;

    public static OAuthAuthorizationApprovalResolution Accepted(ClaimsPrincipal principal) =>
        new(principal, null, false);

    public static OAuthAuthorizationApprovalResolution Rejected(string description) =>
        new(null, description, false);

    public static OAuthAuthorizationApprovalResolution ReplacementRequired(string description) =>
        new(null, description, true);
}

public sealed record OAuthTokenExchangeResolution(
    ClaimsPrincipal? Principal,
    string? Error,
    string? ErrorDescription)
{
    public bool Success => Principal is not null;

    public static OAuthTokenExchangeResolution Accepted(ClaimsPrincipal principal) =>
        new(principal, null, null);

    public static OAuthTokenExchangeResolution Rejected(string error, string description) =>
        new(null, error, description);
}
