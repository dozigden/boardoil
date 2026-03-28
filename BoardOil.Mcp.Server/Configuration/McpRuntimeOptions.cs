namespace BoardOil.Mcp.Server.Configuration;

public sealed class McpRuntimeOptions
{
    public string HttpUrls { get; init; } = "http://0.0.0.0:5001";
    public string ConnectionString { get; init; } = string.Empty;
    public Uri? EventsApiBaseUrl { get; init; }
    public string? EventsApiKey { get; init; }

    public static McpRuntimeOptions FromConfiguration(IConfiguration configuration)
    {
        var httpUrls = configuration["BOARDOIL_MCP_HTTP_URLS"];
        var connectionString = configuration["BOARDOIL_MCP_CONNECTION_STRING"];
        var eventsApiBaseUrl = configuration["BOARDOIL_MCP_EVENTS_API_BASE_URL"];
        var eventsApiKey = configuration["BOARDOIL_MCP_EVENTS_API_KEY"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Set BOARDOIL_MCP_CONNECTION_STRING before running BoardOil.Mcp.Server.");
        }

        Uri? parsedBaseUrl = null;
        if (!string.IsNullOrWhiteSpace(eventsApiBaseUrl))
        {
            if (!Uri.TryCreate(eventsApiBaseUrl, UriKind.Absolute, out parsedBaseUrl))
            {
                throw new InvalidOperationException("BOARDOIL_MCP_EVENTS_API_BASE_URL must be an absolute URI.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(eventsApiKey))
        {
            throw new InvalidOperationException(
                "BOARDOIL_MCP_EVENTS_API_KEY was set without BOARDOIL_MCP_EVENTS_API_BASE_URL.");
        }

        return new McpRuntimeOptions
        {
            HttpUrls = string.IsNullOrWhiteSpace(httpUrls) ? "http://0.0.0.0:5001" : httpUrls,
            ConnectionString = connectionString,
            EventsApiBaseUrl = parsedBaseUrl,
            EventsApiKey = string.IsNullOrWhiteSpace(eventsApiKey) ? null : eventsApiKey
        };
    }
}
