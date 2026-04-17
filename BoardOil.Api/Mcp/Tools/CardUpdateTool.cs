using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Contracts;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardUpdateTool(
    ICardService cardService,
    IMcpAuthorisationService authorisationService) : McpToolBase<CardUpdateInput, CardMutationOutput>(authorisationService)
{
    private readonly ICardService _cardService = cardService;

    public override McpToolDefinition Definition { get; } =
        new(ToolNames.CardUpdate, "Update card title, description, tags, and optional target column.", ToolSchemas.CardUpdateInput, ToolSchemas.ObjectOutput);

    protected override async Task<McpToolResult<CardMutationOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        CardUpdateInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<ValidationError> validationErrors =
        [
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId"),
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.Id, "id"),
            ..McpToolCallHelpers.ValidateOptionalIdentifier(input.ColumnId, "columnId"),
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.CardTypeId, "cardTypeId")
        ];
        if (validationErrors.Count > 0)
        {
            return Failure(validationErrors);
        }

        var boardId = input.BoardId!.Value;
        var cardId = input.Id!.Value;

        var accessError = AuthorisationService.EnsurePatToolAccess(context.PatAccessContext, MachinePatScopes.McpWrite, boardId);
        if (accessError is not null)
        {
            return Failure(accessError);
        }

        var request = new UpdateCardRequest(input.Title, input.Description, input.TagNames, input.CardTypeId!.Value, input.ColumnId);
        var result = await _cardService.UpdateCardAsync(boardId, cardId, request, context.ActorUserId);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        return Success(new CardMutationOutput(result.Data.ToMcp(), "updated"));
    }
}
