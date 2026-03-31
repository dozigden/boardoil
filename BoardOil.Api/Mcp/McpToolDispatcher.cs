using BoardOil.Mcp.Contracts;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace BoardOil.Api.Mcp;

public sealed class McpToolDispatcher(
    McpServiceProviderAccessor serviceProviderAccessor,
    IHttpContextAccessor httpContextAccessor)
{
    private readonly McpServiceProviderAccessor _serviceProviderAccessor = serviceProviderAccessor;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public ValueTask<ListToolsResult> ListToolsAsync(RequestContext<ListToolsRequestParams> _, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tools = ToolCatalogue.Definitions
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
            return McpToolCallHelpers.CreateCallToolResult(new McpToolResult<object>(
                false,
                null,
                new McpToolError("validation_failed", "Tool name is required.", 400)));
        }

        using var scope = _serviceProviderAccessor.ServiceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var patAccessContext = McpPatAccess.TryGetPatAccessContext(_httpContextAccessor.HttpContext?.User);

        if (!McpToolHandlers.ByName.TryGetValue(request.Name, out var handler))
        {
            return McpToolCallHelpers.CreateCallToolResult(new McpToolResult<object>(
                false,
                null,
                new McpToolError("tool_not_found", $"Unknown tool '{request.Name}'.", 404)));
        }

        return await handler(request.Arguments, services, patAccessContext, cancellationToken);
    }
}
