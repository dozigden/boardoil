using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardCommentDeleteTool(
    ICardCommentService cardCommentService,
    IMcpAuthorisationService authorisationService) : McpToolBase<CardCommentDeleteInput, CardCommentMutationOutput>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.CardCommentDelete,
        "Permanently delete a live card comment. Authors can delete their own comments; board owners can delete any. Resolve id from card_get.comments[].id. Already absent comments succeed without further changes. Card attachments are kept. Archived comments cannot be deleted.",
        ToolSchemas.CardCommentDeleteInput,
        ToolSchemas.CardCommentDeleteOutput,
        MachinePatScopes.McpWrite,
        ToolDiscoveryOrder.CardCommentDelete,
        McpToolBehaviours.IdempotentDestructive);

    protected override async Task<McpToolResult<CardCommentMutationOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        CardCommentDeleteInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ValidationError> validationErrors =
        [
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId"),
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.CardId, "cardId"),
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.Id, "id")
        ];
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

        var result = await cardCommentService.DeleteCommentAsync(boardId, input.CardId!.Value, input.Id!.Value, context.ActorUserId);
        if (!result.Success)
        {
            return Failure(result.ToMcpError());
        }

        return Success(new CardCommentMutationOutput(input.Id.Value, "deleted"));
    }
}
