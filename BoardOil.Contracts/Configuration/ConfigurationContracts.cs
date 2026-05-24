namespace BoardOil.Contracts.Configuration;

public sealed record ConfigurationDto(
    bool AllowInsecureCookies,
    string? McpPublicBaseUrl);

public sealed record UpdateConfigurationRequest(string? McpPublicBaseUrl);

public sealed record SystemInfoMessageDto(
    bool Enabled,
    string? Emoji,
    string Title,
    string Description,
    string StyleName,
    string StylePropertiesJson);
