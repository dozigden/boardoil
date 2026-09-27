using System.Text.Json.Serialization;

namespace BoardOil.Contracts.Configuration;

public sealed record ConfigurationDto(
    bool AllowInsecureCookies,
    string? McpPublicBaseUrl,
    [property: JsonPropertyName("oauthLifecycleDiagnosticsEnabled")]
    bool OAuthLifecycleDiagnosticsEnabled,
    [property: JsonPropertyName("oauthLifecycleDiagnosticsRetentionDays")]
    int OAuthLifecycleDiagnosticsRetentionDays,
    string SystemTimeZoneId);

public sealed record UpdateConfigurationRequest(
    string? McpPublicBaseUrl,
    [property: JsonPropertyName("oauthLifecycleDiagnosticsEnabled")]
    bool OAuthLifecycleDiagnosticsEnabled,
    string SystemTimeZoneId);

public sealed record SystemInfoMessageDto(
    bool Enabled,
    string? Emoji,
    string Title,
    string Description,
    string StyleName,
    string StylePropertiesJson);
