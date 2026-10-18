using BoardOil.Abstractions.CardType;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardTypeDeleteTool(
    ICardTypeService cardTypeService,
    IMcpAuthorisationService authorisationService) : McpToolBase<CardTypeIdInput, CardTypeMutationReceipt>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.CardTypeDelete,
        "Delete a non-default card type and reassign its cards to the current default.",
        ToolSchemas.CardTypeIdInput,
        ToolSchemas.CardTypeDeleteOutput,
        MachinePatScopes.McpWrite,
        ToolDiscoveryOrder.CardTypeDelete,
        McpToolBehaviours.IdempotentDestructive);

    protected override async Task<McpToolResult<CardTypeMutationReceipt>> ExecuteCoreAsync(
        McpInvocationContext context,
        CardTypeIdInput input,
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
        var accessError = AuthorisationService.EnsureToolAccess(context.AccessContext, Definition.RequiredScope, boardId);
        if (accessError is not null)
        {
            return Failure(accessError);
        }

        var result = await cardTypeService.DeleteCardTypeAsync(boardId, input.Id!.Value, context.ActorUserId);
        if (!result.Success)
        {
            return Failure(result.ToMcpError());
        }

        return Success(new CardTypeMutationReceipt(input.Id!.Value, "deleted"));
    }
}
