namespace BoardOil.Api.Configuration;

public sealed class BoardOilMcpOptions
{
    public McpTransportMode TransportMode { get; init; } = McpTransportMode.Http;
    public McpAuthMode AuthMode { get; init; } = McpAuthMode.Pat;
    public int AnonymousActorUserId { get; init; } = 1;

    public bool SupportsLegacySseTransport =>
        TransportMode is McpTransportMode.Both;

    public static BoardOilMcpOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("BoardOilMcp");
        return new BoardOilMcpOptions
        {
            TransportMode = ParseTransportMode(section["TransportMode"]),
            AuthMode = ParseEnum(section["AuthMode"], McpAuthMode.Pat),
            AnonymousActorUserId = ParseAnonymousActorUserId(section["AnonymousActorUserId"])
        };
    }

    private static McpTransportMode ParseTransportMode(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return McpTransportMode.Http;
        }

        var normalised = rawValue.Trim();
        if (string.Equals(normalised, "sse", StringComparison.OrdinalIgnoreCase))
        {
            // Backward-compat alias after collapsing modes to http|both.
            return McpTransportMode.Both;
        }

        return ParseEnum(normalised, McpTransportMode.Http);
    }

    private static TEnum ParseEnum<TEnum>(string? rawValue, TEnum defaultValue)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return defaultValue;
        }

        return Enum.TryParse<TEnum>(rawValue.Trim(), true, out var parsed)
            ? parsed
            : defaultValue;
    }

    private static int ParseAnonymousActorUserId(string? rawValue)
    {
        if (!int.TryParse(rawValue, out var parsed) || parsed <= 0)
        {
            return 1;
        }

        return parsed;
    }
}

public enum McpTransportMode
{
    Http = 0,
    Both = 1
}

public enum McpAuthMode
{
    Pat = 0,
    None = 1
}
