using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using BoardOil.Contracts.Auth;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BoardOil.Api.OAuth;

public sealed class OAuthDynamicClientRegistrationService(
    IOpenIddictApplicationManager applicationManager,
    BoardOilOAuthOptions options,
    TimeProvider timeProvider) : IOAuthDynamicClientRegistrationService
{
    private const int MaximumRedirectUriCount = 20;

    internal const string DynamicRegistrationProperty = "boardoil:dynamic_registration";
    internal const string RegistrationExpiresAtProperty = "boardoil:registration_expires_at";

    private static readonly string[] SupportedGrantTypes =
    [
        GrantTypes.AuthorizationCode,
        GrantTypes.RefreshToken
    ];

    private static readonly string[] SupportedResponseTypes = [ResponseTypes.Code];
    private static readonly string[] SupportedScopes = [MachinePatScopes.McpRead, MachinePatScopes.McpWrite];

    public async Task<OAuthDynamicClientRegistrationResult> RegisterAsync(
        OAuthDynamicClientRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return validationError;
        }

        var now = timeProvider.GetUtcNow();
        var clientId = await CreateUniqueClientIdAsync(cancellationToken);
        var clientName = request.ClientName!.Trim();
        var redirectUris = DeduplicateRedirectUris(request.RedirectUris!);
        var grantTypes = ResolveGrantTypes(request.GrantTypes)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(GetGrantTypeOrder)
            .ToArray();
        var applicationType = ResolveApplicationType(request.ApplicationType, redirectUris);
        var scopes = ResolveScopes(request.Scope);

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ApplicationType = applicationType,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Explicit,
            DisplayName = clientName,
        };
        descriptor.RedirectUris.UnionWith(redirectUris);
        descriptor.Permissions.Add(Permissions.Endpoints.Authorization);
        descriptor.Permissions.Add(Permissions.Endpoints.Token);
        descriptor.Permissions.Add(Permissions.GrantTypes.AuthorizationCode);
        descriptor.Permissions.Add(Permissions.ResponseTypes.Code);
        if (grantTypes.Contains(GrantTypes.RefreshToken, StringComparer.Ordinal))
        {
            descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);
        }

        foreach (var scope in scopes)
        {
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
        }

        descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
        descriptor.Properties[DynamicRegistrationProperty] = JsonSerializer.SerializeToElement(true);
        descriptor.Properties[RegistrationExpiresAtProperty] = JsonSerializer.SerializeToElement(
            now.Add(options.DynamicClientRegistrationLifetime));

        await applicationManager.CreateAsync(descriptor, cancellationToken);

        return OAuthDynamicClientRegistrationResult.Accepted(new OAuthDynamicClientRegistrationResponse(
            clientId,
            now.ToUnixTimeSeconds(),
            clientName,
            request.ClientUri,
            redirectUris.Select(static uri => uri.OriginalString).ToArray(),
            grantTypes,
            SupportedResponseTypes,
            ClientAuthenticationMethods.None,
            string.Join(' ', scopes),
            applicationType));
    }

    public async Task<int> CleanupExpiredRegistrationsAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var deleted = 0;
        await foreach (var application in applicationManager.ListAsync(null, null, cancellationToken))
        {
            var properties = await applicationManager.GetPropertiesAsync(application, cancellationToken);
            if (!IsDynamicRegistration(properties)
                || !TryGetExpiry(properties, out var expiresAt)
                || expiresAt > now)
            {
                continue;
            }

            await applicationManager.DeleteAsync(application, cancellationToken);
            deleted++;
        }

        return deleted;
    }

    private async Task<string> CreateUniqueClientIdAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var clientId = $"bo_oauth_{Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant()}";
            if (await applicationManager.FindByClientIdAsync(clientId, cancellationToken) is null)
            {
                return clientId;
            }
        }
    }

    private static OAuthDynamicClientRegistrationResult? Validate(OAuthDynamicClientRegistrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ClientName) || request.ClientName.Trim().Length > 120)
        {
            return InvalidClientMetadata("client_name is required and must be 120 characters or fewer.");
        }

        if (request.ClientUri is not null && !IsSafeWebOrLoopbackUri(request.ClientUri))
        {
            return InvalidClientMetadata("client_uri must be an absolute HTTPS URL or an absolute HTTP loopback URL.");
        }

        if (request.RedirectUris is not { Length: > 0 })
        {
            return InvalidRedirectUri("At least one redirect_uri is required.");
        }

        if (request.RedirectUris.Any(static value => !IsSafeWebOrLoopbackUri(value)))
        {
            return InvalidRedirectUri(
                "Each redirect URI must be an absolute HTTPS URI or an absolute HTTP loopback URI.");
        }

        var redirectUris = DeduplicateRedirectUris(request.RedirectUris);
        if (redirectUris.Length > MaximumRedirectUriCount)
        {
            return InvalidRedirectUri($"No more than {MaximumRedirectUriCount} distinct redirect_uris are allowed.");
        }

        var grantTypes = ResolveGrantTypes(request.GrantTypes);
        if (grantTypes.Length == 0
            || !grantTypes.Contains(GrantTypes.AuthorizationCode, StringComparer.Ordinal)
            || grantTypes.Distinct(StringComparer.Ordinal).Except(SupportedGrantTypes, StringComparer.Ordinal).Any())
        {
            return InvalidClientMetadata("Only authorization_code with optional refresh_token is supported.");
        }

        if (request.ResponseTypes is not null
            && (request.ResponseTypes is not { Length: 1 }
                || !string.Equals(request.ResponseTypes[0], ResponseTypes.Code, StringComparison.Ordinal)))
        {
            return InvalidClientMetadata("Only the code response type is supported.");
        }

        if (!string.Equals(request.TokenEndpointAuthMethod, ClientAuthenticationMethods.None, StringComparison.Ordinal))
        {
            return InvalidClientMetadata("Only public clients using token_endpoint_auth_method none are supported.");
        }

        if (!string.IsNullOrWhiteSpace(request.ApplicationType)
            && !string.Equals(request.ApplicationType, ApplicationTypes.Native, StringComparison.Ordinal)
            && !string.Equals(request.ApplicationType, ApplicationTypes.Web, StringComparison.Ordinal))
        {
            return InvalidClientMetadata("application_type must be native or web.");
        }

        var applicationType = ResolveApplicationType(
            request.ApplicationType,
            redirectUris);
        if (string.Equals(applicationType, ApplicationTypes.Web, StringComparison.Ordinal)
            && redirectUris.Any(IsHttpLoopbackUri))
        {
            return InvalidRedirectUri("Web clients must use HTTPS redirect URIs.");
        }

        var scopes = ResolveScopes(request.Scope);
        if (scopes.Length == 0 || scopes.Except(SupportedScopes, StringComparer.Ordinal).Any())
        {
            return InvalidClientMetadata("Only mcp:read and mcp:write scopes are supported.");
        }

        return null;
    }

    private static bool IsSafeWebOrLoopbackUri(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 2048
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Port is <= 0 or > 65535
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsHttpLoopbackUri(uri);
    }

    private static bool IsHttpLoopbackUri(Uri uri)
    {
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var host = uri.Host.Trim('[', ']');
        return uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6
            && IPAddress.TryParse(host, out var address)
            && IPAddress.IsLoopback(address);
    }

    private static Uri[] DeduplicateRedirectUris(IEnumerable<string> redirectUris)
    {
        var distinctUris = new List<Uri>();
        var seenUris = new HashSet<Uri>();
        foreach (var value in redirectUris)
        {
            var uri = new Uri(value, UriKind.Absolute);
            if (seenUris.Add(uri))
            {
                distinctUris.Add(uri);
            }
        }

        return [.. distinctUris];
    }

    private static string ResolveApplicationType(string? requestedApplicationType, IEnumerable<Uri> redirectUris)
    {
        if (!string.IsNullOrWhiteSpace(requestedApplicationType))
        {
            return requestedApplicationType;
        }

        if (redirectUris.Any(IsHttpLoopbackUri))
        {
            return ApplicationTypes.Native;
        }

        return ApplicationTypes.Web;
    }

    private static string[] ResolveGrantTypes(string[]? grantTypes)
    {
        if (grantTypes is null)
        {
            return [GrantTypes.AuthorizationCode];
        }

        return grantTypes;
    }

    private static string[] ParseScopes(string scope) =>
        scope
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(GetScopeOrder)
            .ToArray();

    private static string[] ResolveScopes(string? scope)
    {
        if (scope is null)
        {
            return [.. SupportedScopes];
        }

        return ParseScopes(scope);
    }

    private static int GetGrantTypeOrder(string grantType) =>
        Array.IndexOf(SupportedGrantTypes, grantType);

    private static int GetScopeOrder(string scope) =>
        Array.IndexOf(SupportedScopes, scope);

    private static bool IsDynamicRegistration(IReadOnlyDictionary<string, JsonElement> properties) =>
        properties.TryGetValue(DynamicRegistrationProperty, out var marker)
        && marker.ValueKind is JsonValueKind.True;

    private static bool TryGetExpiry(
        IReadOnlyDictionary<string, JsonElement> properties,
        out DateTimeOffset expiresAt)
    {
        if (properties.TryGetValue(RegistrationExpiresAtProperty, out var expiry)
            && expiry.ValueKind is JsonValueKind.String
            && expiry.TryGetDateTimeOffset(out expiresAt))
        {
            return true;
        }

        expiresAt = default;
        return false;
    }

    private static OAuthDynamicClientRegistrationResult InvalidClientMetadata(string description) =>
        OAuthDynamicClientRegistrationResult.Rejected("invalid_client_metadata", description);

    private static OAuthDynamicClientRegistrationResult InvalidRedirectUri(string description) =>
        OAuthDynamicClientRegistrationResult.Rejected("invalid_redirect_uri", description);
}
