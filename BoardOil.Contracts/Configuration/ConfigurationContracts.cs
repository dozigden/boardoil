namespace BoardOil.Contracts.Configuration;

public sealed record ConfigurationDto(
    bool AllowInsecureCookies,
    string? McpPublicBaseUrl);

public sealed record UpdateConfigurationRequest(string? McpPublicBaseUrl);
