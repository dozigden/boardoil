using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Contracts;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardCreateTool(
    ICardService cardService,
    IMcpAuthorisationService authorisationService) : McpToolBase<CardCreateInput, CardMutationOutput>(authorisationService)
{
    private readonly ICardService _cardService = cardService;

    public override McpToolDefinition Definition { get; } =
        new(ToolNames.CardCreate, "Create a card in a specific column.", ToolSchemas.CardCreateInput, ToolSchemas.ObjectOutput);

    protected override async Task<McpToolResult<CardMutationOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        CardCreateInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<ValidationError> validationErrors =
        [
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId"),
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.ColumnId, "columnId")
        ];
        if (validationErrors.Count > 0)
        {
            return Failure(validationErrors);
        }

        var boardId = input.BoardId!.Value;
        var columnId = input.ColumnId!.Value;

        var accessError = AuthorisationService.EnsurePatToolAccess(context.PatAccessContext, MachinePatScopes.McpWrite, boardId);
        if (accessError is not null)
        {
            return Failure(accessError);
        }

        var request = new CreateCardRequest(columnId, input.Title, input.Description, input.TagNames);
        var result = await _cardService.CreateCardAsync(boardId, request, context.ActorUserId);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        return Success(new CardMutationOutput(result.Data.ToMcp(), "created"));
    }
}
