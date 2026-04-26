using BoardOil.Mcp.Contracts;
using BoardOil.Api.Auth;
using BoardOil.Api.Configuration;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace BoardOil.Api.Mcp;

public sealed class McpToolDispatcher(
    McpServiceProviderAccessor serviceProviderAccessor,
    IHttpContextAccessor httpContextAccessor,
    McpToolRegistry toolRegistry,
    IMcpAuthorisationService authorisationService,
    BoardOilMcpOptions mcpOptions)
{
    private readonly McpServiceProviderAccessor _serviceProviderAccessor = serviceProviderAccessor;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly McpToolRegistry _toolRegistry = toolRegistry;
    private readonly IMcpAuthorisationService _authorisationService = authorisationService;
    private readonly BoardOilMcpOptions _mcpOptions = mcpOptions;

    public ValueTask<ListToolsResult> ListToolsAsync(RequestContext<ListToolsRequestParams> _, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tools = _toolRegistry.Definitions
            .Select(definition => new Tool
            {
                Name = definition.Name,
                Description = definition.Description,
                InputSchema = McpToolCallHelpers.ParseJson(definition.InputSchemaJson),
                OutputSchema = McpToolCallHelpers.ParseJson(definition.OutputSchemaJson)
            })
            .ToList();

        return ValueTask.FromResult(new ListToolsResult
        {
            Tools = tools
        });
    }

    public async ValueTask<CallToolResult> CallToolAsync(RequestContext<CallToolRequestParams> requestContext, CancellationToken cancellationToken)
    {
        var request = requestContext.Params;
        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            return McpToolCallHelpers.CreateErrorCallToolResult("validation_failed", "Tool name is required.", 400);
        }

        using var scope = _serviceProviderAccessor.ServiceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User.TryGetUserId(out var actorUserId) != true
            && !TryGetAnonymousActorUserId(httpContext, out actorUserId))
        {
            return McpToolCallHelpers.CreateErrorCallToolResult("unauthorized", "Invalid identity context.", 401);
        }

        var patAccessContext = _authorisationService.GetPatAccessContext(httpContext?.User);
        var correlationId = httpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
        var invocationContext = new McpInvocationContext(services, actorUserId, patAccessContext, correlationId);

        if (!_toolRegistry.TryGetRegistration(request.Name, out var registration))
        {
            return McpToolCallHelpers.CreateErrorCallToolResult("tool_not_found", $"Unknown tool '{request.Name}'.", 404);
        }

        if (services.GetRequiredService(registration!.ImplementationType) is not IMcpTool tool)
        {
            return McpToolCallHelpers.CreateErrorCallToolResult("service_error", $"Tool '{request.Name}' is not executable.", 500);
        }

        return await tool.ExecuteAsync(invocationContext, request.Arguments, cancellationToken);
    }

    private bool TryGetAnonymousActorUserId(HttpContext? httpContext, out int actorUserId)
    {
        actorUserId = default;
        if (_mcpOptions.AuthMode is not McpAuthMode.None)
        {
            return false;
        }

        if (httpContext is null)
        {
            return false;
        }

        var path = httpContext.Request.Path;
        var isMcpPath = path.StartsWithSegments("/mcp", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/v1/mcp", StringComparison.OrdinalIgnoreCase)
            || (_mcpOptions.SupportsLegacySseTransport
                && (path.StartsWithSegments("/sse", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWithSegments("/messages", StringComparison.OrdinalIgnoreCase)));
        if (!isMcpPath)
        {
            return false;
        }

        actorUserId = _mcpOptions.AnonymousActorUserId;
        return actorUserId > 0;
    }
}
