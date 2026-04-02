using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Contracts;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardDeleteTool(
    ICardService cardService,
    IMcpAuthorisationService authorisationService) : McpToolBase<CardDeleteInput, CardMutationOutput>(authorisationService)
{
    private readonly ICardService _cardService = cardService;

    public override McpToolDefinition Definition { get; } =
        new(ToolNames.CardDelete, "Delete a card.", ToolSchemas.CardDeleteInput, ToolSchemas.ObjectOutput);

    protected override async Task<McpToolResult<CardMutationOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        CardDeleteInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<ValidationError> validationErrors =
        [
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId"),
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.Id, "id")
        ];
        if (validationErrors.Count > 0)
        {
            return Failure(validationErrors);
        }

        var boardId = input.BoardId!.Value;
        var cardId = input.Id!.Value;

        var accessError = AuthorisationService.EnsurePatToolAccess(context.PatAccessContext, MachinePatScopes.McpWrite, boardId);
        if (accessError is not null)
        {
            return Failure(accessError);
        }

        var result = await _cardService.DeleteCardAsync(boardId, cardId);
        if (!result.Success)
        {
            return Failure(result.ToMcpError());
        }

        return Success(new CardMutationOutput(null, "deleted"));
    }
}
