namespace BoardOil.Api.Configuration;

public sealed class BoardOilInternalOptions
{
    public string? McpEventRelayApiKey { get; init; }

    public static BoardOilInternalOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("BoardOilInternal");
        return new BoardOilInternalOptions
        {
            McpEventRelayApiKey = section["McpEventRelayApiKey"]
        };
    }
}
