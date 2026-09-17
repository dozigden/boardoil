using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardRestoreTool(
    ICardArchiveService cardArchiveService,
    IMcpAuthorisationService authorisationService)
    : McpToolBase<CardRestoreInput, CardMutationOutput>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.CardRestore,
        "Restore an archived card when it is needed on the active board again. The restored card keeps its original board-scoped number.",
        ToolSchemas.CardRestoreInput,
        ToolSchemas.CardRestoreOutput,
        MachinePatScopes.McpWrite,
        ToolDiscoveryOrder.CardRestore,
        McpToolBehaviours.Destructive);

    protected override async Task<McpToolResult<CardMutationOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        CardRestoreInput input,
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
        var accessError = AuthorisationService.EnsureToolAccess(
            context.AccessContext,
            Definition.RequiredScope,
            boardId);
        if (accessError is not null)
        {
            return Failure(accessError);
        }

        var result = await cardArchiveService.UnarchiveCardAsync(boardId, cardId, context.ActorUserId);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        return Success(new CardMutationOutput(result.Data.Id, "restored"));
    }
}
