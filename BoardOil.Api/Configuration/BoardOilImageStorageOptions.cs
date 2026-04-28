using BoardOil.Abstractions.Image;

namespace BoardOil.Api.Configuration;

public sealed class BoardOilImageStorageOptions
{
    public string? RootPath { get; init; }

    public static ImageStorageOptions Resolve(IConfiguration configuration, string connectionString)
    {
        var section = configuration.GetSection("BoardOil");
        var configuredRootPath = section["ImageRootPath"];
        if (!string.IsNullOrWhiteSpace(configuredRootPath))
        {
            var explicitRoot = Path.GetFullPath(configuredRootPath.Trim());
            Directory.CreateDirectory(explicitRoot);
            return new ImageStorageOptions { RootPath = explicitRoot };
        }

        var defaultRoot = BuildDefaultRootPath(connectionString);
        Directory.CreateDirectory(defaultRoot);
        return new ImageStorageOptions { RootPath = defaultRoot };
    }

    private static string BuildDefaultRootPath(string connectionString)
    {
        const string prefix = "Data Source=";
        if (!connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(Path.Combine(".", "data", "images"));
        }

        var dataSource = connectionString[prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(dataSource)
            || string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase)
            || dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(Path.Combine(".", "data", "images"));
        }

        var databasePath = Path.GetFullPath(dataSource);
        var databaseDirectory = Path.GetDirectoryName(databasePath);
        if (string.IsNullOrWhiteSpace(databaseDirectory))
        {
            return Path.GetFullPath(Path.Combine(".", "data", "images"));
        }

        return Path.Combine(databaseDirectory, "images");
    }
}
