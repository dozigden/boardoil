using BoardOil.Api.Configuration;
using BoardOil.Abstractions.Attachment;
using BoardOil.Api.Auth;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;

namespace BoardOil.Api.Mcp;

public static class McpServiceCollectionExtensions
{
    public static IServiceCollection AddBoardOilMcp(this IServiceCollection services, BoardOilMcpOptions mcpOptions)
    {
        var mcpServiceProviderAccessor = new McpServiceProviderAccessor();
        services.AddSingleton(mcpServiceProviderAccessor);

        services.AddSingleton<IMcpAuthorisationService, McpAuthorisationService>();
        services.AddScoped<IAttachmentTransferCredentialValidator, AttachmentTransferCredentialValidator>();
        // The MCP SDK logs complete JSON-RPC messages at Trace, including ticket secrets.
        // Apply to every configured rule so category/provider overrides cannot re-enable that output.
        services.PostConfigure<LoggerFilterOptions>(options =>
        {
            options.Rules.Insert(0, new LoggerFilterRule(null, null, options.MinLevel, null));
            for (var i = 0; i < options.Rules.Count; i++)
            {
                var rule = options.Rules[i];
                options.Rules[i] = new LoggerFilterRule(rule.ProviderName, rule.CategoryName, rule.LogLevel,
                    (provider, category, level) =>
                        !(level == LogLevel.Trace && category?.StartsWith("ModelContextProtocol", StringComparison.Ordinal) == true)
                        && (rule.Filter?.Invoke(provider, category, level) ?? true));
            }
        });
        services.AddSingleton<IMcpErrorResponseFactory, McpErrorResponseFactory>();

        RegisterTool<BoardListTool>(services);
        RegisterTool<BoardGetTool>(services);
        RegisterTool<IdentityGetTool>(services);
        RegisterTool<CardOptionsGetTool>(services);
        RegisterTool<CardGetTool>(services);
        RegisterTool<CardAttachmentListTool>(services);
        RegisterTool<CardAttachmentDeleteTool>(services);
        RegisterTool<CardAttachmentDownloadTool>(services);
        RegisterTool<CardAttachmentUploadTool>(services);
        RegisterTool<CardCreateTool>(services);
        RegisterTool<CardUpdateTool>(services);
        RegisterTool<CardMoveTool>(services);
        RegisterTool<CardDeleteTool>(services);
        RegisterTool<CardCommentCreateTool>(services);
        RegisterTool<TagCreateTool>(services);
        RegisterTool<TagUpdateTool>(services);
        RegisterTool<TagDeleteTool>(services);
        RegisterTool<SlickCreateTool>(services);
        RegisterTool<SlickUpdateTool>(services);
        RegisterTool<SlickDeleteTool>(services);

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
                if (mcpOptions.SupportsLegacySseTransport)
                {
                    options.SessionMode = HttpServerSessionMode.StatefulForInitializeClients;
                }
                else
                {
                    options.SessionMode = HttpServerSessionMode.Stateless;
                }

                options.EnableLegacySse = mcpOptions.SupportsLegacySseTransport;
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
