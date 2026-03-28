using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace BoardOil.Mcp.Server.Tests.Infrastructure;

public sealed class BoardOilMcpFactory(string databasePath) : WebApplicationFactory<Program>
{
    private readonly string _databasePath = databasePath;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var settings = new Dictionary<string, string?>
        {
            ["BOARDOIL_MCP_CONNECTION_STRING"] = $"Data Source={_databasePath}",
            ["BOARDOIL_MCP_HTTP_URLS"] = "http://127.0.0.1:5001",
            ["BoardOilAuth:Issuer"] = "boardoil-test",
            ["BoardOilAuth:Audience"] = "boardoil-test",
            ["BoardOilAuth:SigningKey"] = "boardoil-test-signing-key-change-me-1234567890"
        };

        builder.UseEnvironment("Testing");
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(settings);
        });
    }
}
