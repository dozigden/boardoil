using BoardOil.Abstractions.CardType;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardTypeSetDefaultTool(
    ICardTypeService cardTypeService,
    IMcpAuthorisationService authorisationService) : McpToolBase<CardTypeIdInput, CardTypeMutationReceipt>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.CardTypeSetDefault,
        "Set the default card type for new cards. Existing cards keep their types.",
        ToolSchemas.CardTypeIdInput,
        ToolSchemas.CardTypeSetDefaultOutput,
        MachinePatScopes.McpWrite,
        ToolDiscoveryOrder.CardTypeSetDefault,
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

        var result = await cardTypeService.SetDefaultCardTypeAsync(boardId, input.Id!.Value, context.ActorUserId);
        if (!result.Success)
        {
            return Failure(result.ToMcpError());
        }

        return Success(new CardTypeMutationReceipt(input.Id!.Value, "default_set"));
    }
}
