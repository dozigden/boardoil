using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Card;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Server.Contracts;
using BoardOil.Mcp.Server.Mapping;

namespace BoardOil.Mcp.Server.Tools;

public sealed class CardUpdateToolHandler(ICardService cardService) : IToolHandler<CardUpdateInput, CardMutationOutput>
{
    public string ToolName => ToolNames.CardUpdate;

    public async Task<McpToolResult<CardMutationOutput>> HandleAsync(CardUpdateInput input, CancellationToken cancellationToken)
    {
        var request = new UpdateCardRequest(input.Title, input.Description, input.TagNames);
        var result = await cardService.UpdateCardAsync(input.BoardId, input.CardId, request);
        if (!result.Success || result.Data is null)
        {
            return result.ToMcpFailure<CardMutationOutput>();
        }

        return new CardMutationOutput(result.Data.ToMcp(), "updated").ToMcpSuccess();
    }
}
