using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Card;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Server.Contracts;
using BoardOil.Mcp.Server.Mapping;

namespace BoardOil.Mcp.Server.Tools;

public sealed class CardCreateToolHandler(ICardService cardService) : IToolHandler<CardCreateInput, CardMutationOutput>
{
    public string ToolName => ToolNames.CardCreate;

    public async Task<McpToolResult<CardMutationOutput>> HandleAsync(CardCreateInput input, CancellationToken cancellationToken)
    {
        var request = new CreateCardRequest(input.BoardColumnId, input.Title, input.Description, input.TagNames);
        var result = await cardService.CreateCardAsync(input.BoardId, request);
        if (!result.Success || result.Data is null)
        {
            return result.ToMcpFailure<CardMutationOutput>();
        }

        return new CardMutationOutput(result.Data.ToMcp(), "created").ToMcpSuccess();
    }
}
