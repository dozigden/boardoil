using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.Column;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Column;
using BoardOil.Contracts.Contracts;
using BoardOil.Contracts.Auth;
using BoardOil.Mcp.Contracts;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Security.Claims;
using System.Text.Json;

namespace BoardOil.Api.Mcp;

public sealed class McpToolDispatcher(
    McpServiceProviderAccessor serviceProviderAccessor,
    IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions SerialiserOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

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
        var patAccessContext = TryGetPatAccessContext(_httpContextAccessor.HttpContext?.User);

        return request.Name switch
            {
            ToolNames.BoardGet => await InvokeAsync<BoardGetInput, McpBoardSnapshot>(request.Arguments, cancellationToken, input =>
                HandleBoardGetAsync(services, input, patAccessContext)),
            ToolNames.ColumnsList => await InvokeAsync<ColumnsListInput, ColumnsListOutput>(request.Arguments, cancellationToken, input =>
                HandleColumnsListAsync(services, input, patAccessContext)),
            ToolNames.CardCreate => await InvokeAsync<CardCreateInput, CardMutationOutput>(request.Arguments, cancellationToken, input =>
                HandleCardCreateAsync(services, input, patAccessContext)),
            ToolNames.CardUpdate => await InvokeAsync<CardUpdateInput, CardMutationOutput>(request.Arguments, cancellationToken, input =>
                HandleCardUpdateAsync(services, input, patAccessContext)),
            ToolNames.CardMove => await InvokeAsync<CardMoveInput, CardMutationOutput>(request.Arguments, cancellationToken, input =>
                HandleCardMoveAsync(services, input, patAccessContext)),
            ToolNames.CardMoveByColumnName => await InvokeAsync<CardMoveByColumnNameInput, CardMutationOutput>(request.Arguments, cancellationToken, input =>
                HandleCardMoveByColumnNameAsync(services, input, patAccessContext)),
            ToolNames.CardDelete => await InvokeAsync<CardDeleteInput, CardMutationOutput>(request.Arguments, cancellationToken, input =>
                HandleCardDeleteAsync(services, input, patAccessContext)),
            _ => CreateCallToolResult(new McpToolResult<object>(
                false,
                null,
                new McpToolError("tool_not_found", $"Unknown tool '{request.Name}'.", 404)))
        };
    }

    private static async Task<CallToolResult> InvokeAsync<TInput, TOutput>(
        IDictionary<string, JsonElement>? arguments,
        CancellationToken cancellationToken,
        Func<TInput, Task<McpToolResult<TOutput>>> handler)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var inputParseResult = ParseArguments<TInput>(arguments);
        if (!inputParseResult.Success || inputParseResult.Input is null)
        {
            return CreateCallToolResult(inputParseResult.ErrorResult);
        }

        McpToolResult<TOutput> result;
        try
        {
            result = await handler(inputParseResult.Input);
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

    private static async Task<McpToolResult<McpBoardSnapshot>> HandleBoardGetAsync(
        IServiceProvider services,
        BoardGetInput input,
        PatAccessContext? patAccessContext)
    {
        var patAccessFailure = EnsurePatToolAccess<McpBoardSnapshot>(patAccessContext, MachinePatScopes.McpRead, input.BoardId);
        if (patAccessFailure is not null)
        {
            return patAccessFailure;
        }

        var boardService = services.GetRequiredService<IBoardService>();
        var result = await boardService.GetBoardAsync(input.BoardId);
        if (!result.Success || result.Data is null)
        {
            return result.ToMcpFailure<McpBoardSnapshot>();
        }

        return result.Data.ToMcp().ToMcpSuccess();
    }

    private static async Task<McpToolResult<ColumnsListOutput>> HandleColumnsListAsync(
        IServiceProvider services,
        ColumnsListInput input,
        PatAccessContext? patAccessContext)
    {
        var patAccessFailure = EnsurePatToolAccess<ColumnsListOutput>(patAccessContext, MachinePatScopes.McpRead, input.BoardId);
        if (patAccessFailure is not null)
        {
            return patAccessFailure;
        }

        var columnService = services.GetRequiredService<IColumnService>();
        var result = await columnService.GetColumnsAsync(input.BoardId);
        if (!result.Success || result.Data is null)
        {
            return result.ToMcpFailure<ColumnsListOutput>();
        }

        var output = new ColumnsListOutput(
            input.BoardId,
            result.Data
                .Select(column => new McpColumnReference(column.Id, column.Title, column.SortKey))
                .ToArray());

        return output.ToMcpSuccess();
    }

    private static async Task<McpToolResult<CardMutationOutput>> HandleCardCreateAsync(
        IServiceProvider services,
        CardCreateInput input,
        PatAccessContext? patAccessContext)
    {
        var patAccessFailure = EnsurePatToolAccess<CardMutationOutput>(patAccessContext, MachinePatScopes.McpWrite, input.BoardId);
        if (patAccessFailure is not null)
        {
            return patAccessFailure;
        }

        var cardService = services.GetRequiredService<ICardService>();
        var request = new CreateCardRequest(input.BoardColumnId, input.Title, input.Description, input.TagNames);
        var result = await cardService.CreateCardAsync(input.BoardId, request);
        if (!result.Success || result.Data is null)
        {
            return result.ToMcpFailure<CardMutationOutput>();
        }

        return new CardMutationOutput(result.Data.ToMcp(), "created").ToMcpSuccess();
    }

    private static async Task<McpToolResult<CardMutationOutput>> HandleCardUpdateAsync(
        IServiceProvider services,
        CardUpdateInput input,
        PatAccessContext? patAccessContext)
    {
        var patAccessFailure = EnsurePatToolAccess<CardMutationOutput>(patAccessContext, MachinePatScopes.McpWrite, input.BoardId);
        if (patAccessFailure is not null)
        {
            return patAccessFailure;
        }

        var cardService = services.GetRequiredService<ICardService>();
        var request = new UpdateCardRequest(input.Title, input.Description, input.TagNames);
        var result = await cardService.UpdateCardAsync(input.BoardId, input.CardId, request);
        if (!result.Success || result.Data is null)
        {
            return result.ToMcpFailure<CardMutationOutput>();
        }

        return new CardMutationOutput(result.Data.ToMcp(), "updated").ToMcpSuccess();
    }

    private static async Task<McpToolResult<CardMutationOutput>> HandleCardMoveAsync(
        IServiceProvider services,
        CardMoveInput input,
        PatAccessContext? patAccessContext)
    {
        var patAccessFailure = EnsurePatToolAccess<CardMutationOutput>(patAccessContext, MachinePatScopes.McpWrite, input.BoardId);
        if (patAccessFailure is not null)
        {
            return patAccessFailure;
        }

        var cardService = services.GetRequiredService<ICardService>();
        var request = new MoveCardRequest(input.BoardColumnId, input.PositionAfterCardId);
        var result = await cardService.MoveCardAsync(input.BoardId, input.CardId, request);
        if (!result.Success || result.Data is null)
        {
            return result.ToMcpFailure<CardMutationOutput>();
        }

        return new CardMutationOutput(result.Data.ToMcp(), "moved").ToMcpSuccess();
    }

    private static async Task<McpToolResult<CardMutationOutput>> HandleCardMoveByColumnNameAsync(
        IServiceProvider services,
        CardMoveByColumnNameInput input,
        PatAccessContext? patAccessContext)
    {
        var patAccessFailure = EnsurePatToolAccess<CardMutationOutput>(patAccessContext, MachinePatScopes.McpWrite, input.BoardId);
        if (patAccessFailure is not null)
        {
            return patAccessFailure;
        }

        var boardService = services.GetRequiredService<IBoardService>();
        var cardService = services.GetRequiredService<ICardService>();

        var boardResult = await boardService.GetBoardAsync(input.BoardId);
        if (!boardResult.Success || boardResult.Data is null)
        {
            return boardResult.ToMcpFailure<CardMutationOutput>();
        }

        var matches = boardResult.Data.Columns
            .Where(column => string.Equals(column.Title, input.ColumnTitle, StringComparison.OrdinalIgnoreCase))
            .Select(column => column.Id)
            .Distinct()
            .ToArray();

        if (matches.Length == 0)
        {
            return new McpToolResult<CardMutationOutput>(
                false,
                null,
                new McpToolError("column_not_found", $"No column named '{input.ColumnTitle}' exists on board {input.BoardId}.", 404));
        }

        if (matches.Length > 1)
        {
            return new McpToolResult<CardMutationOutput>(
                false,
                null,
                new McpToolError("column_ambiguous", $"Multiple columns named '{input.ColumnTitle}' exist on board {input.BoardId}. Move by column id instead.", 400));
        }

        var moveResult = await cardService.MoveCardAsync(input.BoardId, input.CardId, new MoveCardRequest(matches[0], input.PositionAfterCardId));
        if (!moveResult.Success || moveResult.Data is null)
        {
            return moveResult.ToMcpFailure<CardMutationOutput>();
        }

        return new CardMutationOutput(moveResult.Data.ToMcp(), "moved").ToMcpSuccess();
    }

    private static async Task<McpToolResult<CardMutationOutput>> HandleCardDeleteAsync(
        IServiceProvider services,
        CardDeleteInput input,
        PatAccessContext? patAccessContext)
    {
        var patAccessFailure = EnsurePatToolAccess<CardMutationOutput>(patAccessContext, MachinePatScopes.McpWrite, input.BoardId);
        if (patAccessFailure is not null)
        {
            return patAccessFailure;
        }

        var cardService = services.GetRequiredService<ICardService>();
        var result = await cardService.DeleteCardAsync(input.BoardId, input.CardId);
        if (!result.Success)
        {
            return result.ToMcpFailure<CardMutationOutput>();
        }

        return new CardMutationOutput(null, "deleted").ToMcpSuccess();
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

    private static McpToolResult<T>? EnsurePatToolAccess<T>(PatAccessContext? patAccessContext, string requiredScope, int boardId)
    {
        if (patAccessContext is null)
        {
            return null;
        }

        if (!patAccessContext.Scopes.Contains(requiredScope))
        {
            return new McpToolResult<T>(
                false,
                default,
                new McpToolError("forbidden", $"PAT token requires scope '{requiredScope}' for this tool.", 403));
        }

        if (!string.Equals(patAccessContext.BoardAccessMode, MachinePatBoardAccessModes.All, StringComparison.Ordinal)
            && !patAccessContext.AllowedBoardIds.Contains(boardId))
        {
            return new McpToolResult<T>(
                false,
                default,
                new McpToolError("forbidden", $"PAT token is not allowed to access board {boardId}.", 403));
        }

        return null;
    }

    private static PatAccessContext? TryGetPatAccessContext(ClaimsPrincipal? claimsPrincipal)
    {
        if (claimsPrincipal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var authType = claimsPrincipal.FindFirst("boardoil_auth_type")?.Value;
        if (!string.Equals(authType, "pat", StringComparison.Ordinal))
        {
            return null;
        }

        var scopes = claimsPrincipal
            .FindAll("boardoil_pat_scope")
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);

        var boardAccessMode = claimsPrincipal.FindFirst("boardoil_pat_board_access_mode")?.Value;
        boardAccessMode = string.IsNullOrWhiteSpace(boardAccessMode)
            ? MachinePatBoardAccessModes.All
            : boardAccessMode.Trim().ToLowerInvariant();

        var allowedBoardIdsClaim = claimsPrincipal.FindFirst("boardoil_pat_allowed_board_ids")?.Value;
        var allowedBoardIds = (allowedBoardIdsClaim ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var boardId) ? boardId : (int?)null)
            .Where(x => x is > 0)
            .Select(x => x!.Value)
            .ToHashSet();

        return new PatAccessContext(scopes, boardAccessMode, allowedBoardIds);
    }

    private sealed record PatAccessContext(
        ISet<string> Scopes,
        string BoardAccessMode,
        ISet<int> AllowedBoardIds);
}

public static class McpMappingExtensions
{
    public static McpBoardSnapshot ToMcp(this BoardDto board) =>
        new(
            board.Id,
            board.Name,
            board.UpdatedAtUtc,
            board.Columns
                .Select(column => new McpColumnSnapshot(
                    column.Id,
                    column.Title,
                    column.SortKey,
                    column.Cards.Select(card => card.ToMcp()).ToArray()))
                .ToArray());

    public static McpCardSnapshot ToMcp(this CardDto card) =>
        new(
            card.Id,
            card.BoardColumnId,
            card.Title,
            card.Description,
            card.SortKey,
            card.TagNames,
            card.UpdatedAtUtc);

    public static McpToolResult<T> ToMcpFailure<T>(this ApiResult apiResult)
    {
        var code = apiResult.StatusCode switch
        {
            400 => "validation_failed",
            401 => "unauthorised",
            403 => "forbidden",
            404 => "not_found",
            _ => "service_error"
        };

        IReadOnlyDictionary<string, IReadOnlyList<string>>? validation = null;
        if (apiResult.ValidationErrors is not null)
        {
            validation = apiResult.ValidationErrors.ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<string>)x.Value);
        }

        return new McpToolResult<T>(
            false,
            default,
            new McpToolError(
                code,
                apiResult.Message ?? "Service returned an error.",
                apiResult.StatusCode,
                validation));
    }

    public static McpToolResult<T> ToMcpSuccess<T>(this T payload) =>
        new(true, payload, null);
}
