using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardCommentUpdateTool(
    ICardCommentService cardCommentService,
    IMcpAuthorisationService authorisationService) : McpToolBase<CardCommentUpdateInput, CardCommentMutationOutput>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.CardCommentUpdate,
        "Replace the text of your own live card comment. Resolve id from card_get.comments[].id. Last save wins; archived comments cannot be edited.",
        ToolSchemas.CardCommentUpdateInput,
        ToolSchemas.CardCommentUpdateOutput,
        MachinePatScopes.McpWrite,
        ToolDiscoveryOrder.CardCommentUpdate,
        McpToolBehaviours.IdempotentDestructive);

    protected override async Task<McpToolResult<CardCommentMutationOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        CardCommentUpdateInput input,
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

        var result = await cardCommentService.UpdateCommentAsync(
            boardId, input.CardId!.Value, input.Id!.Value, new UpdateCardCommentRequest(input.Text), context.ActorUserId);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        return Success(new CardCommentMutationOutput(result.Data.Id, "updated"));
    }
}
