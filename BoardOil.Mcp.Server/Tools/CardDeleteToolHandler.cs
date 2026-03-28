using BoardOil.Abstractions.Card;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Server.Contracts;
using BoardOil.Mcp.Server.Mapping;

namespace BoardOil.Mcp.Server.Tools;

public sealed class CardDeleteToolHandler(ICardService cardService) : IToolHandler<CardDeleteInput, CardMutationOutput>
{
    public string ToolName => ToolNames.CardDelete;

    public async Task<McpToolResult<CardMutationOutput>> HandleAsync(CardDeleteInput input, CancellationToken cancellationToken)
    {
        var result = await cardService.DeleteCardAsync(input.BoardId, input.CardId);
        if (!result.Success)
        {
            return result.ToMcpFailure<CardMutationOutput>();
        }

        return new CardMutationOutput(null, "deleted").ToMcpSuccess();
    }
}
