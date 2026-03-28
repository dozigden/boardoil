using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Server.Contracts;
using BoardOil.Mcp.Server.Tools;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Text.Json;

namespace BoardOil.Mcp.Server.Mcp;

public sealed class McpToolDispatcher(McpServiceProviderAccessor serviceProviderAccessor)
{
    private static readonly JsonSerializerOptions SerialiserOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly McpServiceProviderAccessor _serviceProviderAccessor = serviceProviderAccessor;

    public ValueTask<ListToolsResult> ListToolsAsync(RequestContext<ListToolsRequestParams> _, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tools = ToolCatalogue.Definitions
            .Select(definition => new Tool
            {
                Name = definition.Name,
                Description = definition.Description,
                InputSchema = ParseJson(definition.InputSchemaJson),
                OutputSchema = ParseJson(definition.OutputSchemaJson)
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
            return CreateCallToolResult(new McpToolResult<object>(
                false,
                null,
                new McpToolError("validation_failed", "Tool name is required.", 400)));
        }

        using var scope = _serviceProviderAccessor.ServiceProvider.CreateScope();
        var services = scope.ServiceProvider;

        return request.Name switch
        {
            ToolNames.BoardGet => await InvokeAsync<BoardGetToolHandler, BoardGetInput, McpBoardSnapshot>(services, request.Arguments, cancellationToken),
            ToolNames.ColumnsList => await InvokeAsync<ColumnsListToolHandler, ColumnsListInput, ColumnsListOutput>(services, request.Arguments, cancellationToken),
            ToolNames.CardCreate => await InvokeAsync<CardCreateToolHandler, CardCreateInput, CardMutationOutput>(services, request.Arguments, cancellationToken),
            ToolNames.CardUpdate => await InvokeAsync<CardUpdateToolHandler, CardUpdateInput, CardMutationOutput>(services, request.Arguments, cancellationToken),
            ToolNames.CardMove => await InvokeAsync<CardMoveToolHandler, CardMoveInput, CardMutationOutput>(services, request.Arguments, cancellationToken),
            ToolNames.CardMoveByColumnName => await InvokeAsync<CardMoveByColumnNameToolHandler, CardMoveByColumnNameInput, CardMutationOutput>(services, request.Arguments, cancellationToken),
            ToolNames.CardDelete => await InvokeAsync<CardDeleteToolHandler, CardDeleteInput, CardMutationOutput>(services, request.Arguments, cancellationToken),
            _ => CreateCallToolResult(new McpToolResult<object>(
                false,
                null,
                new McpToolError("tool_not_found", $"Unknown tool '{request.Name}'.", 404)))
        };
    }

    private static async Task<CallToolResult> InvokeAsync<THandler, TInput, TOutput>(
        IServiceProvider services,
        IDictionary<string, JsonElement>? arguments,
        CancellationToken cancellationToken)
        where THandler : IToolHandler<TInput, TOutput>
    {
        var inputParseResult = ParseArguments<TInput>(arguments);
        if (!inputParseResult.Success || inputParseResult.Input is null)
        {
            return CreateCallToolResult(inputParseResult.ErrorResult);
        }

        var handler = services.GetRequiredService<THandler>();
        McpToolResult<TOutput> result;
        try
        {
            result = await handler.HandleAsync(inputParseResult.Input, cancellationToken);
        }
        catch (Exception ex)
        {
            result = new McpToolResult<TOutput>(
                false,
                default,
                new McpToolError("service_error", $"Tool execution failed: {ex.Message}", 500));
        }

        return CreateCallToolResult(result);
    }

    private static (bool Success, TInput? Input, McpToolResult<object> ErrorResult) ParseArguments<TInput>(IDictionary<string, JsonElement>? arguments)
    {
        try
        {
            var argumentsJson = JsonSerializer.Serialize(arguments ?? new Dictionary<string, JsonElement>(), SerialiserOptions);
            var parsed = JsonSerializer.Deserialize<TInput>(argumentsJson, SerialiserOptions);
            if (parsed is null)
            {
                return (
                    false,
                    default,
                    new McpToolResult<object>(
                        false,
                        null,
                        new McpToolError("validation_failed", "Tool arguments are required.", 400)));
            }

            return (true, parsed, new McpToolResult<object>(true, null, null));
        }
        catch (JsonException ex)
        {
            return (
                false,
                default,
                new McpToolResult<object>(
                    false,
                    null,
                    new McpToolError("validation_failed", $"Invalid tool arguments: {ex.Message}", 400)));
        }
    }

    private static CallToolResult CreateCallToolResult<TPayload>(McpToolResult<TPayload> result)
    {
        var payloadJson = JsonSerializer.SerializeToElement(result, SerialiserOptions);
        var text = result.Success ? "ok" : result.Error?.Message ?? "Tool call failed.";

        return new CallToolResult
        {
            IsError = !result.Success,
            StructuredContent = payloadJson,
            Content =
            [
                new TextContentBlock
                {
                    Text = text
                }
            ]
        };
    }

    private static JsonElement ParseJson(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }
}
