using BoardOil.Abstractions.Attachment;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardAttachmentDeleteTool(
    ICardAttachmentService attachmentService,
    IMcpAuthorisationService authorisationService)
    : McpToolBase<CardAttachmentDeleteInput, CardAttachmentDeleteOutput>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.CardAttachmentDelete,
        "Permanently delete one attachment from a saved live card. Resolve id from card_attachment_list.items[].id or card_get.attachments[].id. Archived attachments cannot be deleted.",
        ToolSchemas.CardAttachmentDeleteInput, ToolSchemas.CardAttachmentDeleteOutput, MachinePatScopes.McpWrite,
        ToolDiscoveryOrder.CardAttachmentDelete);

    protected override async Task<McpToolResult<CardAttachmentDeleteOutput>> ExecuteCoreAsync(
        McpInvocationContext context, CardAttachmentDeleteInput input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ValidationError> validationErrors =
        [
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId"),
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.CardId, "cardId"),
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.Id, "id")
        ];
        if (validationErrors.Count > 0) { return Failure(validationErrors); }

        var boardId = input.BoardId!.Value;
        var accessError = AuthorisationService.EnsureToolAccess(context.AccessContext, Definition.RequiredScope, boardId);
        if (accessError is not null) { return Failure(accessError); }

        var result = await attachmentService.DeleteAsync(boardId, input.CardId!.Value, input.Id!.Value, context.ActorUserId);
        if (!result.Success) { return Failure(result.ToMcpError()); }
        return Success(new CardAttachmentDeleteOutput(input.Id.Value, "deleted"));
    }
}
