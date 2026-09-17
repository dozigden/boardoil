using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardArchiveTool(
    ICardArchiveService cardArchiveService,
    IMcpAuthorisationService authorisationService)
    : McpToolBase<CardArchiveInput, CardMutationOutput>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.CardArchive,
        "Remove a no-longer-needed card from the active board by archiving it. The archived card remains inspectable and can be restored if needed.",
        ToolSchemas.CardArchiveInput,
        ToolSchemas.CardArchiveOutput,
        MachinePatScopes.McpWrite,
        ToolDiscoveryOrder.CardArchive,
        McpToolBehaviours.Destructive);

    protected override async Task<McpToolResult<CardMutationOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        CardArchiveInput input,
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

        var result = await cardArchiveService.ArchiveCardAsync(boardId, cardId, context.ActorUserId);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        return Success(new CardMutationOutput(result.Data.Id, "archived"));
    }
}
