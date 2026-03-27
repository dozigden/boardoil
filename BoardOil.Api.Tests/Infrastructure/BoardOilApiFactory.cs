using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace BoardOil.Api.Tests.Infrastructure;

public sealed class BoardOilApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath;
    private readonly bool _allowInsecureCookies;

    public BoardOilApiFactory(string databasePath, bool allowInsecureCookies = true)
    {
        _databasePath = databasePath;
        _allowInsecureCookies = allowInsecureCookies;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        builder.UseEnvironment("Testing");
        builder.UseSetting("ASPNETCORE_URLS", "http://127.0.0.1:5000");
        builder.UseSetting("ConnectionStrings:BoardOil", $"Data Source={_databasePath}");
        builder.UseSetting("BoardOil:DataPath", _databasePath);
        builder.UseSetting("BoardOil:ExposeLan", "false");
        builder.UseSetting("BoardOil:Port", "5000");
        builder.UseSetting("BoardOilAuth:AllowInsecureCookies", _allowInsecureCookies.ToString().ToLowerInvariant());

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:BoardOil"] = $"Data Source={_databasePath}",
                ["BoardOil:DataPath"] = _databasePath,
                ["BoardOil:ExposeLan"] = "false",
                ["BoardOil:Port"] = "5000",
                ["BoardOilAuth:AllowInsecureCookies"] = _allowInsecureCookies.ToString().ToLowerInvariant()
            };
            configBuilder.AddInMemoryCollection(settings);
        });
    }
}
