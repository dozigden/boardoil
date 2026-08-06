namespace BoardOil.Dev;

internal static class DevApiLaunchSettings
{
    public const int HttpPort = 5000;
    public const int HttpsPort = 5001;
    public const string HttpEndpoint = "http://127.0.0.1:5000";
    public const string HttpsEndpoint = "https://localhost:5001";
    public const string ListenUrls = HttpEndpoint + ";" + HttpsEndpoint;

    public static IReadOnlyList<int> Ports => [HttpPort, HttpsPort];

    public static IReadOnlyList<string> CreateRunArguments(string apiProject) =>
    [
        "run",
        "--no-launch-profile",
        "--no-build",
        "--project",
        apiProject
    ];

    public static IReadOnlyDictionary<string, string> CreateEnvironment(string databasePath) =>
        new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["DOTNET_ENVIRONMENT"] = "Development",
            ["ASPNETCORE_URLS"] = ListenUrls,
            ["ConnectionStrings__BoardOil"] = $"Data Source={databasePath}"
        };
}
