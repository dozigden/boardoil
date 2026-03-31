using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.Column;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Column;
using BoardOil.Mcp.Contracts;
using ModelContextProtocol.Protocol;
using System.Text.Json;

namespace BoardOil.Api.Mcp;

internal delegate Task<CallToolResult> McpToolHandler(
    IDictionary<string, JsonElement>? arguments,
    IServiceProvider services,
    PatAccessContext? patAccessContext,
    CancellationToken cancellationToken);

internal static class McpToolHandlers
{
    public static readonly IReadOnlyDictionary<string, McpToolHandler> ByName = new Dictionary<string, McpToolHandler>(StringComparer.Ordinal)
    {
        [ToolNames.BoardGet] = HandleBoardGetCallAsync,
        [ToolNames.ColumnsList] = HandleColumnsListCallAsync,
        [ToolNames.CardCreate] = HandleCardCreateCallAsync,
        [ToolNames.CardUpdate] = HandleCardUpdateCallAsync,
        [ToolNames.CardMove] = HandleCardMoveCallAsync,
        [ToolNames.CardMoveByColumnName] = HandleCardMoveByColumnNameCallAsync,
        [ToolNames.CardDelete] = HandleCardDeleteCallAsync
    };

    private static Task<CallToolResult> HandleBoardGetCallAsync(
        IDictionary<string, JsonElement>? arguments,
        IServiceProvider services,
        PatAccessContext? patAccessContext,
        CancellationToken cancellationToken) =>
        McpToolCallHelpers.InvokeAsync<BoardGetInput, McpBoardSnapshot>(
            arguments,
            cancellationToken,
            input => HandleBoardGetAsync(services, input, patAccessContext));

    private static Task<CallToolResult> HandleColumnsListCallAsync(
        IDictionary<string, JsonElement>? arguments,
        IServiceProvider services,
        PatAccessContext? patAccessContext,
        CancellationToken cancellationToken) =>
        McpToolCallHelpers.InvokeAsync<ColumnsListInput, ColumnsListOutput>(
            arguments,
            cancellationToken,
            input => HandleColumnsListAsync(services, input, patAccessContext));

    private static Task<CallToolResult> HandleCardCreateCallAsync(
        IDictionary<string, JsonElement>? arguments,
        IServiceProvider services,
        PatAccessContext? patAccessContext,
        CancellationToken cancellationToken) =>
        McpToolCallHelpers.InvokeAsync<CardCreateInput, CardMutationOutput>(
            arguments,
            cancellationToken,
            input => HandleCardCreateAsync(services, input, patAccessContext));

    private static Task<CallToolResult> HandleCardUpdateCallAsync(
        IDictionary<string, JsonElement>? arguments,
        IServiceProvider services,
        PatAccessContext? patAccessContext,
        CancellationToken cancellationToken) =>
        McpToolCallHelpers.InvokeAsync<CardUpdateInput, CardMutationOutput>(
            arguments,
            cancellationToken,
            input => HandleCardUpdateAsync(services, input, patAccessContext));

    private static Task<CallToolResult> HandleCardMoveCallAsync(
        IDictionary<string, JsonElement>? arguments,
        IServiceProvider services,
        PatAccessContext? patAccessContext,
        CancellationToken cancellationToken) =>
        McpToolCallHelpers.InvokeAsync<CardMoveInput, CardMutationOutput>(
            arguments,
            cancellationToken,
            input => HandleCardMoveAsync(services, input, patAccessContext));

    private static Task<CallToolResult> HandleCardMoveByColumnNameCallAsync(
        IDictionary<string, JsonElement>? arguments,
        IServiceProvider services,
        PatAccessContext? patAccessContext,
        CancellationToken cancellationToken) =>
        McpToolCallHelpers.InvokeAsync<CardMoveByColumnNameInput, CardMutationOutput>(
            arguments,
            cancellationToken,
            input => HandleCardMoveByColumnNameAsync(services, input, patAccessContext));

    private static Task<CallToolResult> HandleCardDeleteCallAsync(
        IDictionary<string, JsonElement>? arguments,
        IServiceProvider services,
        PatAccessContext? patAccessContext,
        CancellationToken cancellationToken) =>
        McpToolCallHelpers.InvokeAsync<CardDeleteInput, CardMutationOutput>(
            arguments,
            cancellationToken,
            input => HandleCardDeleteAsync(services, input, patAccessContext));

    private static async Task<McpToolResult<McpBoardSnapshot>> HandleBoardGetAsync(
        IServiceProvider services,
        BoardGetInput input,
        PatAccessContext? patAccessContext)
    {
        var boardIdValidation = McpToolCallHelpers.ValidateRequiredIdentifier(input.Id, "id");
        if (boardIdValidation is not null)
        {
            return boardIdValidation.ToMcpFailure<McpBoardSnapshot>();
        }

        var boardId = input.Id!.Value;

        var patAccessFailure = McpPatAccess.EnsurePatToolAccess<McpBoardSnapshot>(patAccessContext, MachinePatScopes.McpRead, boardId);
        if (patAccessFailure is not null)
        {
            return patAccessFailure;
        }

        var boardService = services.GetRequiredService<IBoardService>();
        var result = await boardService.GetBoardAsync(boardId);
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
        var boardIdValidation = McpToolCallHelpers.ValidateRequiredIdentifier(input.Id, "id");
        if (boardIdValidation is not null)
        {
            return boardIdValidation.ToMcpFailure<ColumnsListOutput>();
        }

        var boardId = input.Id!.Value;

        var patAccessFailure = McpPatAccess.EnsurePatToolAccess<ColumnsListOutput>(patAccessContext, MachinePatScopes.McpRead, boardId);
        if (patAccessFailure is not null)
        {
            return patAccessFailure;
        }

        var columnService = services.GetRequiredService<IColumnService>();
        var result = await columnService.GetColumnsAsync(boardId);
        if (!result.Success || result.Data is null)
        {
            return result.ToMcpFailure<ColumnsListOutput>();
        }

        var output = new ColumnsListOutput(
            boardId,
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
        var boardIdValidation = McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId");
        if (boardIdValidation is not null)
        {
            return boardIdValidation.ToMcpFailure<CardMutationOutput>();
        }

        var columnIdValidation = McpToolCallHelpers.ValidateRequiredIdentifier(input.ColumnId, "columnId");
        if (columnIdValidation is not null)
        {
            return columnIdValidation.ToMcpFailure<CardMutationOutput>();
        }

        var boardId = input.BoardId!.Value;
        var columnId = input.ColumnId!.Value;

        var patAccessFailure = McpPatAccess.EnsurePatToolAccess<CardMutationOutput>(patAccessContext, MachinePatScopes.McpWrite, boardId);
        if (patAccessFailure is not null)
        {
            return patAccessFailure;
        }

        var cardService = services.GetRequiredService<ICardService>();
        var request = new CreateCardRequest(columnId, input.Title, input.Description, input.TagNames);
        var result = await cardService.CreateCardAsync(boardId, request);
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
        var boardIdValidation = McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId");
        if (boardIdValidation is not null)
        {
            return boardIdValidation.ToMcpFailure<CardMutationOutput>();
        }

        var cardIdValidation = McpToolCallHelpers.ValidateRequiredIdentifier(input.Id, "id");
        if (cardIdValidation is not null)
        {
            return cardIdValidation.ToMcpFailure<CardMutationOutput>();
        }

        var boardId = input.BoardId!.Value;
        var cardId = input.Id!.Value;

        var patAccessFailure = McpPatAccess.EnsurePatToolAccess<CardMutationOutput>(patAccessContext, MachinePatScopes.McpWrite, boardId);
        if (patAccessFailure is not null)
        {
            return patAccessFailure;
        }

        var cardService = services.GetRequiredService<ICardService>();
        var request = new UpdateCardRequest(input.Title, input.Description, input.TagNames);
        var result = await cardService.UpdateCardAsync(boardId, cardId, request);
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
        var boardIdValidation = McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId");
        if (boardIdValidation is not null)
        {
            return boardIdValidation.ToMcpFailure<CardMutationOutput>();
        }

        var cardIdValidation = McpToolCallHelpers.ValidateRequiredIdentifier(input.Id, "id");
        if (cardIdValidation is not null)
        {
            return cardIdValidation.ToMcpFailure<CardMutationOutput>();
        }

        var columnIdValidation = McpToolCallHelpers.ValidateRequiredIdentifier(input.ColumnId, "columnId");
        if (columnIdValidation is not null)
        {
            return columnIdValidation.ToMcpFailure<CardMutationOutput>();
        }

        var afterIdValidation = McpToolCallHelpers.ValidateOptionalIdentifier(input.AfterId, "afterId");
        if (afterIdValidation is not null)
        {
            return afterIdValidation.ToMcpFailure<CardMutationOutput>();
        }

        var boardId = input.BoardId!.Value;
        var cardId = input.Id!.Value;
        var columnId = input.ColumnId!.Value;
        var afterId = input.AfterId;

        var patAccessFailure = McpPatAccess.EnsurePatToolAccess<CardMutationOutput>(patAccessContext, MachinePatScopes.McpWrite, boardId);
        if (patAccessFailure is not null)
        {
            return patAccessFailure;
        }

        var cardService = services.GetRequiredService<ICardService>();
        var request = new MoveCardRequest(columnId, afterId);
        var result = await cardService.MoveCardAsync(boardId, cardId, request);
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
        var boardIdValidation = McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId");
        if (boardIdValidation is not null)
        {
            return boardIdValidation.ToMcpFailure<CardMutationOutput>();
        }

        var cardIdValidation = McpToolCallHelpers.ValidateRequiredIdentifier(input.Id, "id");
        if (cardIdValidation is not null)
        {
            return cardIdValidation.ToMcpFailure<CardMutationOutput>();
        }

        var afterIdValidation = McpToolCallHelpers.ValidateOptionalIdentifier(input.AfterId, "afterId");
        if (afterIdValidation is not null)
        {
            return afterIdValidation.ToMcpFailure<CardMutationOutput>();
        }

        var boardId = input.BoardId!.Value;
        var cardId = input.Id!.Value;
        var afterId = input.AfterId;

        var patAccessFailure = McpPatAccess.EnsurePatToolAccess<CardMutationOutput>(patAccessContext, MachinePatScopes.McpWrite, boardId);
        if (patAccessFailure is not null)
        {
            return patAccessFailure;
        }

        var boardService = services.GetRequiredService<IBoardService>();
        var cardService = services.GetRequiredService<ICardService>();

        var boardResult = await boardService.GetBoardAsync(boardId);
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
                new McpToolError("column_not_found", $"No column named '{input.ColumnTitle}' exists on board {boardId}.", 404));
        }

        if (matches.Length > 1)
        {
            return new McpToolResult<CardMutationOutput>(
                false,
                null,
                new McpToolError("column_ambiguous", $"Multiple columns named '{input.ColumnTitle}' exist on board {boardId}. Move by column id instead.", 400));
        }

        var moveResult = await cardService.MoveCardAsync(boardId, cardId, new MoveCardRequest(matches[0], afterId));
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
        var boardIdValidation = McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId");
        if (boardIdValidation is not null)
        {
            return boardIdValidation.ToMcpFailure<CardMutationOutput>();
        }

        var cardIdValidation = McpToolCallHelpers.ValidateRequiredIdentifier(input.Id, "id");
        if (cardIdValidation is not null)
        {
            return cardIdValidation.ToMcpFailure<CardMutationOutput>();
        }

        var boardId = input.BoardId!.Value;
        var cardId = input.Id!.Value;

        var patAccessFailure = McpPatAccess.EnsurePatToolAccess<CardMutationOutput>(patAccessContext, MachinePatScopes.McpWrite, boardId);
        if (patAccessFailure is not null)
        {
            return patAccessFailure;
        }

        var cardService = services.GetRequiredService<ICardService>();
        var result = await cardService.DeleteCardAsync(boardId, cardId);
        if (!result.Success)
        {
            return result.ToMcpFailure<CardMutationOutput>();
        }

        return new CardMutationOutput(null, "deleted").ToMcpSuccess();
    }
}
