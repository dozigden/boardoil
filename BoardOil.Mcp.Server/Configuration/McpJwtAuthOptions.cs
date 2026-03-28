namespace BoardOil.Mcp.Server.Configuration;

public sealed class McpJwtAuthOptions
{
    public string Issuer { get; init; } = "boardoil";
    public string Audience { get; init; } = "boardoil";
    public string SigningKey { get; init; } = string.Empty;

    public static McpJwtAuthOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("BoardOilAuth");
        var options = new McpJwtAuthOptions
        {
            Issuer = section["Issuer"] ?? "boardoil",
            Audience = section["Audience"] ?? "boardoil",
            SigningKey = section["SigningKey"] ?? "replace-this-with-a-strong-32-char-min-signing-key"
        };

        if (options.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "BoardOilAuth:SigningKey must be at least 32 characters. Set a strong key in configuration.");
        }

        return options;
    }
}
