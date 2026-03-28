using System.Net;

namespace BoardOil.Api.Configuration;

public sealed class BoardOilInternalOptions
{
    public string? McpEventRelayApiKey { get; init; }
    public IReadOnlyList<IPAddress> McpEventRelayAllowedSourceIps { get; init; } = [];

    public static BoardOilInternalOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("BoardOilInternal");
        return new BoardOilInternalOptions
        {
            McpEventRelayApiKey = section["McpEventRelayApiKey"],
            McpEventRelayAllowedSourceIps = ParseIpList(section["McpEventRelayAllowedSourceIps"])
        };
    }

    private static IReadOnlyList<IPAddress> ParseIpList(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return [];
        }

        var values = rawValue.Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var parsed = new List<IPAddress>(values.Length);
        foreach (var value in values)
        {
            if (!IPAddress.TryParse(value, out var address))
            {
                continue;
            }

            parsed.Add(Normalise(address));
        }

        return parsed;
    }

    private static IPAddress Normalise(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}
