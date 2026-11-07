using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Card;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardArchiveTool(
    ICardArchiveService cards,
    IMcpAuthorisationService authorisationService)
    : McpToolBase<CardSelectionInput, ArchiveCardsSummaryDto>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.CardArchive,
        "Archive one or more live cards from the active board. Requires a non-empty list of unique positive board-scoped card numbers; every card must exist or none are archived. Archived cards remain inspectable and can be restored if needed.",
        ToolSchemas.CardArchiveInput,
        ToolSchemas.CardArchiveOutput,
        MachinePatScopes.McpWrite,
        ToolDiscoveryOrder.CardArchive,
        McpToolBehaviours.Destructive);

    protected override async Task<McpToolResult<ArchiveCardsSummaryDto>> ExecuteCoreAsync(
        McpInvocationContext context,
        CardSelectionInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validationErrors = McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId");
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

        var result = await cards.ArchiveCardsAsync(boardId, new ArchiveCardsRequest(input.CardIds), context.ActorUserId);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        return Success(result.Data);
    }
}
