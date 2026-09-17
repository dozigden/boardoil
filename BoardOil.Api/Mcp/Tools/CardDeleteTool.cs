using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardDeleteTool(
    ICardService cardService,
    IMcpAuthorisationService authorisationService) : McpToolBase<CardDeleteInput, CardMutationOutput>(authorisationService)
{
    private readonly ICardService _cardService = cardService;

    public override McpToolDefinition Definition { get; } =
        new(ToolNames.CardDelete, "Permanently delete a live card. Prefer card_archive when recoverable removal is appropriate or permanent deletion was not explicitly requested.", ToolSchemas.CardDeleteInput, ToolSchemas.CardDeleteOutput, MachinePatScopes.McpWrite, ToolDiscoveryOrder.CardDelete, McpToolBehaviours.IdempotentDestructive);

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

        var accessError = AuthorisationService.EnsureToolAccess(context.AccessContext, Definition.RequiredScope, boardId);
        if (accessError is not null)
        {
            return Failure(accessError);
        }

        var result = await _cardService.DeleteCardAsync(boardId, cardId, context.ActorUserId);
        if (!result.Success)
        {
            return Failure(result.ToMcpError());
        }

        return Success(new CardMutationOutput(cardId, "deleted"));
    }
}
