using BoardOil.Abstractions;
using BoardOil.Contracts.Realtime;
using BoardOil.Mcp.Server.Realtime;
using BoardOil.Mcp.Server.Tools;
using BoardOil.Services.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var connectionString = Environment.GetEnvironmentVariable("BOARDOIL_MCP_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Set BOARDOIL_MCP_CONNECTION_STRING before running BoardOil.Mcp.Server.");
}

var services = new ServiceCollection();
services.AddBoardOilServices(connectionString);
ConfigureBoardEvents(services);

services.AddTransient<BoardGetToolHandler>();
services.AddTransient<ColumnsListToolHandler>();
services.AddTransient<CardCreateToolHandler>();
services.AddTransient<CardUpdateToolHandler>();
services.AddTransient<CardMoveToolHandler>();
services.AddTransient<CardMoveByColumnNameToolHandler>();
services.AddTransient<CardDeleteToolHandler>();
services.AddSingleton<ToolRegistry>();

using var provider = services.BuildServiceProvider();

var registry = provider.GetRequiredService<ToolRegistry>();
Console.WriteLine("BoardOil MCP scaffold ready. Registered tools:");
foreach (var tool in registry.ListTools())
{
    Console.WriteLine($" - {tool.Name}");
}

static void ConfigureBoardEvents(IServiceCollection services)
{
    var apiBaseUrl = Environment.GetEnvironmentVariable("BOARDOIL_MCP_EVENTS_API_BASE_URL");
    var apiKey = Environment.GetEnvironmentVariable("BOARDOIL_MCP_EVENTS_API_KEY");

    var hasBaseUrl = !string.IsNullOrWhiteSpace(apiBaseUrl);
    var hasApiKey = !string.IsNullOrWhiteSpace(apiKey);

    if (!hasBaseUrl)
    {
        if (hasApiKey)
        {
            throw new InvalidOperationException(
                "BOARDOIL_MCP_EVENTS_API_KEY was set without BOARDOIL_MCP_EVENTS_API_BASE_URL.");
        }

        services.AddSingleton<IBoardEvents, NoOpBoardEvents>();
        Console.WriteLine("BoardOil MCP realtime forwarding disabled (no API base URL configured).");
        return;
    }

    if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var baseUri))
    {
        throw new InvalidOperationException("BOARDOIL_MCP_EVENTS_API_BASE_URL must be an absolute URI.");
    }

    services.AddSingleton(new HttpClient
    {
        BaseAddress = baseUri,
        Timeout = TimeSpan.FromSeconds(5)
    });
    services.AddSingleton<IBoardEvents>(_ => new ApiForwardingBoardEvents(
        _.GetRequiredService<HttpClient>(),
        apiKey));

    var relayTarget = $"{baseUri.ToString().TrimEnd('/')}{BoardRealtimeRelay.EndpointPath}";
    var authMode = hasApiKey ? "api-key" : "source-ip";
    Console.WriteLine($"BoardOil MCP realtime forwarding enabled ({authMode}) -> {relayTarget}");
}
