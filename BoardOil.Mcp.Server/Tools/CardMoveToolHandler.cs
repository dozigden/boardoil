using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Card;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Server.Contracts;
using BoardOil.Mcp.Server.Mapping;

namespace BoardOil.Mcp.Server.Tools;

public sealed class CardMoveToolHandler(ICardService cardService) : IToolHandler<CardMoveInput, CardMutationOutput>
{
    public string ToolName => ToolNames.CardMove;

    public async Task<McpToolResult<CardMutationOutput>> HandleAsync(CardMoveInput input, CancellationToken cancellationToken)
    {
        var request = new MoveCardRequest(input.BoardColumnId, input.PositionAfterCardId);
        var result = await cardService.MoveCardAsync(input.BoardId, input.CardId, request);
        if (!result.Success || result.Data is null)
        {
            return result.ToMcpFailure<CardMutationOutput>();
        }

        return new CardMutationOutput(result.Data.ToMcp(), "moved").ToMcpSuccess();
    }
}
