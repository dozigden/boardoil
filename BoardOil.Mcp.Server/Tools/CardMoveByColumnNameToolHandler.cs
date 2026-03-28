using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Card;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Server.Contracts;
using BoardOil.Mcp.Server.Mapping;

namespace BoardOil.Mcp.Server.Tools;

public sealed class CardMoveByColumnNameToolHandler(
    IBoardService boardService,
    ICardService cardService)
    : IToolHandler<CardMoveByColumnNameInput, CardMutationOutput>
{
    public string ToolName => ToolNames.CardMoveByColumnName;

    public async Task<McpToolResult<CardMutationOutput>> HandleAsync(CardMoveByColumnNameInput input, CancellationToken cancellationToken)
    {
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
}
