using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardCreateTool(
    ICardService cardService,
    IMcpAuthorisationService authorisationService) : McpToolBase<CardCreateInput, CardMutationOutput>(authorisationService)
{
    private readonly ICardService _cardService = cardService;

    public override McpToolDefinition Definition { get; } =
        new(ToolNames.CardCreate, "Create a card in a specific column. Use card_options_get to resolve valid IDs and existing tag or slick names. BoardOil-hosted images require creating the card first, then using card_attachment_upload and card_update.", ToolSchemas.CardCreateInput, ToolSchemas.CardCreateOutput, MachinePatScopes.McpWrite, ToolDiscoveryOrder.CardCreate, McpToolBehaviours.NonDestructive);

    protected override async Task<McpToolResult<CardMutationOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        CardCreateInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<ValidationError> validationErrors =
        [
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId"),
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.ColumnId, "columnId"),
            ..McpToolCallHelpers.ValidateOptionalIdentifier(input.AssignedUserId, "assignedUserId")
        ];
        if (validationErrors.Count > 0)
        {
            return Failure(validationErrors);
        }

        var boardId = input.BoardId!.Value;
        var columnId = input.ColumnId!.Value;

        var accessError = AuthorisationService.EnsureToolAccess(context.AccessContext, Definition.RequiredScope, boardId);
        if (accessError is not null)
        {
            return Failure(accessError);
        }

        var request = new CreateCardRequest(
            columnId,
            input.Title,
            input.Description,
            input.TagNames,
            input.CardTypeId,
            input.AssignedUserId,
            input.SlickName,
            input.ExternalUrl);
        var result = await _cardService.CreateCardAsync(boardId, request, context.ActorUserId);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        return Success(new CardMutationOutput(result.Data.Id, "created"));
    }
}
