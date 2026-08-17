using System.Net;
using System.Net.Sockets;

namespace BoardOil.Dev;

internal static class DevApiLaunchSettings
{
    public const int HttpPort = 5000;
    public const int HttpsPort = 5001;
    public const string HttpEndpoint = "http://127.0.0.1:5000";
    public const string LanHttpEndpoint = "http://0.0.0.0:5000";
    public const string HttpsEndpoint = "https://localhost:5001";

    public static IReadOnlyList<int> Ports => [HttpPort, HttpsPort];

    public static string CreateDisplayEndpoint(bool exposeLan)
    {
        if (!exposeLan)
        {
            return HttpsEndpoint;
        }

        try
        {
            var lanAddress = Dns.GetHostAddresses(Dns.GetHostName())
                .FirstOrDefault(static address =>
                    address.AddressFamily == AddressFamily.InterNetwork
                    && !IPAddress.IsLoopback(address));
            return CreateLanDisplayEndpoint(lanAddress?.ToString());
        }
        catch (SocketException)
        {
            return CreateLanDisplayEndpoint(null);
        }
    }

    internal static string CreateLanDisplayEndpoint(string? lanAddress)
    {
        if (string.IsNullOrWhiteSpace(lanAddress))
        {
            return $"{HttpsEndpoint} | LAN address unavailable";
        }

        return $"{HttpsEndpoint} | http://{lanAddress}:{HttpPort}";
    }

    public static IReadOnlyList<string> CreateRunArguments(string apiProject) =>
    [
        "run",
        "--no-launch-profile",
        "--no-build",
        "--project",
        apiProject
    ];

    public static IReadOnlyDictionary<string, string> CreateEnvironment(
        string databasePath,
        bool exposeLan = false) =>
        new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["DOTNET_ENVIRONMENT"] = "Development",
            ["ASPNETCORE_URLS"] = $"{(exposeLan ? LanHttpEndpoint : HttpEndpoint)};{HttpsEndpoint}",
            ["ConnectionStrings__BoardOil"] = $"Data Source={databasePath}"
        };
}
