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
