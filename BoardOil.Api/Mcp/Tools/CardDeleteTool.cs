using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardDeleteTool(
    ICardService cards,
    IMcpAuthorisationService authorisationService)
    : McpToolBase<CardSelectionInput, BulkDeleteCardsSummaryDto>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.CardDelete,
        "Permanently delete one or more live cards from one board. Prefer card_archive when recoverable removal is appropriate or permanent deletion was not explicitly requested. Duplicate IDs are counted once, missing cards are skipped, and an empty list is a no-op.",
        ToolSchemas.CardDeleteInput,
        ToolSchemas.CardDeleteOutput,
        MachinePatScopes.McpWrite,
        ToolDiscoveryOrder.CardDelete,
        McpToolBehaviours.IdempotentDestructive);

    protected override async Task<McpToolResult<BulkDeleteCardsSummaryDto>> ExecuteCoreAsync(
        McpInvocationContext context,
        CardSelectionInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validationErrors = McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId").ToList();
        if (input.CardIds is null)
        {
            validationErrors.Add(new ValidationError("cardIds", "Card IDs are required."));
        }
        else if (input.CardIds.Any(cardId => cardId <= 0))
        {
            validationErrors.Add(new ValidationError("cardIds", "Card IDs must be greater than 0."));
        }

        if (validationErrors.Count > 0)
        {
            return Failure(validationErrors);
        }

        var boardId = input.BoardId!.Value;
        var accessError = AuthorisationService.EnsureToolAccess(context.AccessContext, Definition.RequiredScope, boardId);
        if (accessError is not null)
        {
            return Failure(accessError);
        }

        var result = await cards.BulkDeleteCardsAsync(boardId, new BulkDeleteCardsRequest(input.CardIds), context.ActorUserId);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        return Success(result.Data);
    }
}
