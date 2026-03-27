namespace BoardOil.Api.Configuration;

public sealed class BoardOilRuntimeOptions
{
    public string? DataPath { get; init; }
    public bool ExposeLan { get; init; }
    public int Port { get; init; } = 5000;

    public static BoardOilRuntimeOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("BoardOil");
        return new BoardOilRuntimeOptions
        {
            DataPath = section["DataPath"],
            ExposeLan = section.GetValue<bool>("ExposeLan"),
            Port = section.GetValue<int?>("Port") ?? 5000
        };
    }

    public string ResolveConnectionString(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString("BoardOil");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var dataPath = string.IsNullOrWhiteSpace(DataPath)
            ? "/data/boardoil.db"
            : DataPath!.Trim();
        return $"Data Source={dataPath}";
    }

    public string ResolveListenUrl(IConfiguration configuration)
    {
        var explicitUrls = configuration["ASPNETCORE_URLS"];
        if (!string.IsNullOrWhiteSpace(explicitUrls))
        {
            return NormalizeListenUrls(explicitUrls);
        }

        var host = ExposeLan ? "0.0.0.0" : "127.0.0.1";
        return $"http://{host}:{Port}";
    }

    private static string NormalizeListenUrls(string explicitUrls)
    {
        // Kestrel does not allow localhost:0 dynamic binding; rewrite to loopback IPs.
        return explicitUrls
            .Replace("http://localhost:0", "http://127.0.0.1:0", StringComparison.OrdinalIgnoreCase)
            .Replace("https://localhost:0", "https://127.0.0.1:0", StringComparison.OrdinalIgnoreCase);
    }
}
