namespace BoardOil.Dev;

internal static class DevWebLaunchSettings
{
    public const string Endpoint = "http://localhost:5173";

    public static IReadOnlyDictionary<string, string> CreateEnvironment() =>
        new Dictionary<string, string>
        {
            ["VITE_BO_OAUTH_PROXY_TARGET"] = DevApiLaunchSettings.HttpsEndpoint
        };
}
