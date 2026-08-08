using BoardOil.Abstractions.Auth;
using BoardOil.Ef.DependencyInjection;
using BoardOil.Services.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace BoardOil.Api.Tests.Infrastructure;

public sealed class BoardOilApiFactory : WebApplicationFactory<Program>
{
    public const string DefaultSigningKey = "boardoil-api-tests-signing-key-12345678901234567890";

    private static readonly object DatabaseTemplateLock = new();
    private static readonly Lazy<SqliteConnection> DatabaseTemplate = new(CreateDatabaseTemplate);

    private readonly string _databasePath;
    private readonly bool _allowInsecureCookies;
    private readonly string? _mcpEventRelayApiKey;
    private readonly string? _mcpEventRelayAllowedSourceIps;
    private readonly IReadOnlyDictionary<string, string?> _configurationOverrides;
    private readonly Action<IServiceCollection>? _configureTestServices;

    public BoardOilApiFactory(
        string databasePath,
        bool allowInsecureCookies = true,
        string? mcpEventRelayApiKey = null,
        string? mcpEventRelayAllowedSourceIps = null,
        IReadOnlyDictionary<string, string?>? configurationOverrides = null,
        Action<IServiceCollection>? configureTestServices = null)
    {
        _databasePath = databasePath;
        _allowInsecureCookies = allowInsecureCookies;
        _mcpEventRelayApiKey = mcpEventRelayApiKey;
        _mcpEventRelayAllowedSourceIps = mcpEventRelayAllowedSourceIps;
        _configurationOverrides = configurationOverrides ?? new Dictionary<string, string?>();
        _configureTestServices = configureTestServices;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var directory = Path.GetDirectoryName(_databasePath);
        var imageRootPath = string.IsNullOrWhiteSpace(directory)
            ? Path.Combine(".", "data", "images")
            : Path.Combine(directory, "images");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
        Directory.CreateDirectory(imageRootPath);

        builder.UseEnvironment("Testing");
        builder.UseSetting("ASPNETCORE_URLS", "http://127.0.0.1:5000");
        builder.UseSetting("ConnectionStrings:BoardOil", $"Data Source={_databasePath}");
        builder.UseSetting("BoardOil:DataPath", _databasePath);
        builder.UseSetting("BoardOil:ImageRootPath", imageRootPath);
        builder.UseSetting("BoardOil:ExposeLan", "false");
        builder.UseSetting("BoardOil:Port", "5000");
        builder.UseSetting("BoardOilAuth:SigningKey", DefaultSigningKey);
        builder.UseSetting("BoardOilAuth:AllowInsecureCookies", _allowInsecureCookies.ToString().ToLowerInvariant());
        if (!string.IsNullOrWhiteSpace(_mcpEventRelayApiKey))
        {
            builder.UseSetting("BoardOilInternal:McpEventRelayApiKey", _mcpEventRelayApiKey);
        }
        if (!string.IsNullOrWhiteSpace(_mcpEventRelayAllowedSourceIps))
        {
            builder.UseSetting("BoardOilInternal:McpEventRelayAllowedSourceIps", _mcpEventRelayAllowedSourceIps);
        }
        foreach (var overrideEntry in _configurationOverrides)
        {
            builder.UseSetting(overrideEntry.Key, overrideEntry.Value);
        }

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:BoardOil"] = $"Data Source={_databasePath}",
                ["BoardOil:DataPath"] = _databasePath,
                ["BoardOil:ImageRootPath"] = imageRootPath,
                ["BoardOil:ExposeLan"] = "false",
                ["BoardOil:Port"] = "5000",
                ["BoardOilAuth:SigningKey"] = DefaultSigningKey,
                ["BoardOilAuth:AllowInsecureCookies"] = _allowInsecureCookies.ToString().ToLowerInvariant()
            };
            if (!string.IsNullOrWhiteSpace(_mcpEventRelayApiKey))
            {
                settings["BoardOilInternal:McpEventRelayApiKey"] = _mcpEventRelayApiKey;
            }
            if (!string.IsNullOrWhiteSpace(_mcpEventRelayAllowedSourceIps))
            {
                settings["BoardOilInternal:McpEventRelayAllowedSourceIps"] = _mcpEventRelayAllowedSourceIps;
            }
            foreach (var overrideEntry in _configurationOverrides)
            {
                settings[overrideEntry.Key] = overrideEntry.Value;
            }

            configBuilder.AddInMemoryCollection(settings);
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPasswordHashService>();
            services.AddSingleton<IPasswordHashService, FastPasswordHashService>();
            _configureTestServices?.Invoke(services);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        InitialiseDatabaseFromTemplate();
        return base.CreateHost(builder);
    }

    private void InitialiseDatabaseFromTemplate()
    {
        if (File.Exists(_databasePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        lock (DatabaseTemplateLock)
        {
            if (File.Exists(_databasePath))
            {
                return;
            }

            ResetDatabaseFromTemplateUnsafe();
        }
    }

    internal void ResetDatabaseFromTemplate()
    {
        lock (DatabaseTemplateLock)
        {
            ResetDatabaseFromTemplateUnsafe();
        }
    }

    private void ResetDatabaseFromTemplateUnsafe()
    {
        using var destination = new SqliteConnection($"Data Source={_databasePath}");
        destination.Open();
        DatabaseTemplate.Value.BackupDatabase(destination);
    }

    private static SqliteConnection CreateDatabaseTemplate()
    {
        var connection = new SqliteConnection(
            $"Data Source=file:boardoil-api-tests-template-{Guid.NewGuid():N}?mode=memory&cache=shared");
        connection.Open();

        try
        {
            using var serviceProvider = new ServiceCollection()
                .AddBoardOilServices()
                .AddBoardOilEfInfrastructure(connection.ConnectionString)
                .BuildServiceProvider();
            serviceProvider.InitializeBoardOilEfInfrastructureAsync().GetAwaiter().GetResult();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
