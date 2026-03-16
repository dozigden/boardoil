namespace BoardOil.Api.Configuration;

public sealed class JwtAuthOptions
{
    public string Issuer { get; init; } = "boardoil";
    public string Audience { get; init; } = "boardoil";
    public string SigningKey { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 14;
    public string AccessTokenCookieName { get; init; } = "boardoil_access";
    public string RefreshTokenCookieName { get; init; } = "boardoil_refresh";

    public static JwtAuthOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("BoardOilAuth");
        var options = new JwtAuthOptions
        {
            Issuer = section["Issuer"] ?? "boardoil",
            Audience = section["Audience"] ?? "boardoil",
            SigningKey = section["SigningKey"] ?? string.Empty,
            AccessTokenMinutes = Math.Max(1, section.GetValue<int?>("AccessTokenMinutes") ?? 15),
            RefreshTokenDays = Math.Max(1, section.GetValue<int?>("RefreshTokenDays") ?? 14),
            AccessTokenCookieName = section["AccessTokenCookieName"] ?? "boardoil_access",
            RefreshTokenCookieName = section["RefreshTokenCookieName"] ?? "boardoil_refresh"
        };

        if (options.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "BoardOilAuth:SigningKey must be at least 32 characters. Set a strong key in configuration.");
        }

        return options;
    }
}
