using BoardOil.Abstractions.Attachment;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardAttachmentListTool(
    ICardAttachmentService attachmentService,
    IMcpAuthorisationService authorisationService)
    : McpToolBase<CardAttachmentListInput, CardAttachmentListDto>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.CardAttachmentList,
        "List attachment metadata and the per-file upload limit for a saved card. Set archived to true for archived cards; archived attachments are read-only. Does not return file contents.",
        ToolSchemas.CardAttachmentListInput, ToolSchemas.CardAttachmentListOutput, MachinePatScopes.McpRead,
        ToolDiscoveryOrder.CardAttachmentList, McpToolBehaviours.ReadOnly);

    protected override async Task<McpToolResult<CardAttachmentListDto>> ExecuteCoreAsync(
        McpInvocationContext context, CardAttachmentListInput input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ValidationError> validationErrors =
        [
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId"),
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.CardId, "cardId")
        ];
        if (validationErrors.Count > 0) { return Failure(validationErrors); }

        var boardId = input.BoardId!.Value;
        var accessError = AuthorisationService.EnsureToolAccess(context.AccessContext, Definition.RequiredScope, boardId);
        if (accessError is not null) { return Failure(accessError); }

        var result = await attachmentService.ListAsync(boardId, input.CardId!.Value, input.Archived, context.ActorUserId);
        if (!result.Success || result.Data is null) { return Failure(result.ToMcpError()); }
        return Success(result.Data);
    }
}
