using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Contracts;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardGetTool(
    ICardService cardService,
    IMcpAuthorisationService authorisationService) : McpToolBase<CardGetInput, McpCardSnapshot>(authorisationService)
{
    private readonly ICardService _cardService = cardService;

    public override McpToolDefinition Definition { get; } =
        new(ToolNames.CardGet, "Get a card snapshot including description and tags.", ToolSchemas.CardGetInput, ToolSchemas.ObjectOutput);

    protected override async Task<McpToolResult<McpCardSnapshot>> ExecuteCoreAsync(
        McpInvocationContext context,
        CardGetInput input,
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

        var accessError = AuthorisationService.EnsurePatToolAccess(context.PatAccessContext, MachinePatScopes.McpRead, boardId);
        if (accessError is not null)
        {
            return Failure(accessError);
        }

        var result = await _cardService.GetCardAsync(boardId, cardId, context.ActorUserId);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        return Success(result.Data.ToMcp());
    }
}
