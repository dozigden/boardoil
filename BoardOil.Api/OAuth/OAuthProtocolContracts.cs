using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoardOil.Api.OAuth;

public sealed record OAuthDynamicClientRegistrationRequest
{
    [JsonPropertyName("client_name")]
    public string? ClientName { get; init; }

    [JsonPropertyName("redirect_uris")]
    public string[]? RedirectUris { get; init; }

    [JsonPropertyName("grant_types")]
    public string[]? GrantTypes { get; init; }

    [JsonPropertyName("response_types")]
    public string[]? ResponseTypes { get; init; }

    [JsonPropertyName("token_endpoint_auth_method")]
    public string? TokenEndpointAuthMethod { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    [JsonPropertyName("application_type")]
    public string? ApplicationType { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalMetadata { get; init; }
}

public sealed record OAuthDynamicClientRegistrationResponse(
    [property: JsonPropertyName("client_id")] string ClientId,
    [property: JsonPropertyName("client_id_issued_at")] long ClientIdIssuedAt,
    [property: JsonPropertyName("client_name")] string ClientName,
    [property: JsonPropertyName("redirect_uris")] IReadOnlyList<string> RedirectUris,
    [property: JsonPropertyName("grant_types")] IReadOnlyList<string> GrantTypes,
    [property: JsonPropertyName("response_types")] IReadOnlyList<string> ResponseTypes,
    [property: JsonPropertyName("token_endpoint_auth_method")] string TokenEndpointAuthMethod,
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("application_type")] string ApplicationType);

public sealed record OAuthProtocolError(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("error_description")] string ErrorDescription);

public sealed record OAuthProtectedResourceMetadata(
    [property: JsonPropertyName("resource")] string Resource,
    [property: JsonPropertyName("authorization_servers")] IReadOnlyList<string> AuthorizationServers,
    [property: JsonPropertyName("scopes_supported")] IReadOnlyList<string> ScopesSupported,
    [property: JsonPropertyName("bearer_methods_supported")] IReadOnlyList<string> BearerMethodsSupported);

public sealed record OAuthDynamicClientRegistrationResult(
    bool Success,
    OAuthDynamicClientRegistrationResponse? Registration,
    OAuthProtocolError? Error)
{
    public static OAuthDynamicClientRegistrationResult Accepted(OAuthDynamicClientRegistrationResponse registration) =>
        new(true, registration, null);

    public static OAuthDynamicClientRegistrationResult Rejected(string error, string description) =>
        new(false, null, new OAuthProtocolError(error, description));
}
