using ModelContextProtocol.Protocol;

namespace BoardOil.Api.Mcp;

public static class McpServiceCollectionExtensions
{
    public static IServiceCollection AddBoardOilMcp(this IServiceCollection services)
    {
        var mcpServiceProviderAccessor = new McpServiceProviderAccessor();
        services.AddSingleton(mcpServiceProviderAccessor);

        services.AddSingleton<IMcpAuthorisationService, McpAuthorisationService>();
        services.AddSingleton<IMcpErrorResponseFactory, McpErrorResponseFactory>();

        RegisterTool<BoardListTool>(services);
        RegisterTool<BoardGetTool>(services);
        RegisterTool<ColumnsListTool>(services);
        RegisterTool<CardGetTool>(services);
        RegisterTool<CardCreateTool>(services);
        RegisterTool<CardUpdateTool>(services);
        RegisterTool<CardMoveTool>(services);
        RegisterTool<CardDeleteTool>(services);

        services.AddSingleton<McpToolRegistry>();
        services.AddSingleton<McpToolDispatcher>();

        services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new ModelContextProtocol.Protocol.Implementation
                {
                    Name = "BoardOil MCP",
                    Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0"
                };
            })
#pragma warning disable MCP9004
            .WithHttpTransport(options =>
            {
                options.Stateless = true;
                options.EnableLegacySse = false;
            })
#pragma warning restore MCP9004
            .WithListToolsHandler((request, cancellationToken) =>
                mcpServiceProviderAccessor
                    .ServiceProvider
                    .GetRequiredService<McpToolDispatcher>()
                    .ListToolsAsync(request, cancellationToken))
            .WithListPromptsHandler((_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(new ListPromptsResult
                {
                    Prompts = []
                });
            })
            .WithListResourcesHandler((_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(new ListResourcesResult
                {
                    Resources = []
                });
            })
            .WithCallToolHandler((request, cancellationToken) =>
                mcpServiceProviderAccessor
                    .ServiceProvider
                    .GetRequiredService<McpToolDispatcher>()
                    .CallToolAsync(request, cancellationToken));

        return services;
    }

    private static void RegisterTool<TTool>(IServiceCollection services)
        where TTool : class, IMcpTool
    {
        services.AddScoped<TTool>();
        services.AddScoped<IMcpTool>(serviceProvider => serviceProvider.GetRequiredService<TTool>());
    }
}
