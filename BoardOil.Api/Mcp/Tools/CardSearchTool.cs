using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Card;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardSearchTool(
    ICardService cardService,
    IMcpAuthorisationService authorisationService)
    : McpToolBase<CardSearchInput, CardTextSearchResultDto>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.CardSearch,
        "Search live cards on a board by case-insensitive literal substring in card number, title, description, or external URL. Tags and slicks are not searched. Returns paginated summaries in board order; use card_get for full detail.",
        ToolSchemas.CardSearchInput, ToolSchemas.CardSearchOutput, MachinePatScopes.McpRead,
        ToolDiscoveryOrder.CardSearch, McpToolBehaviours.ReadOnly);

    protected override async Task<McpToolResult<CardTextSearchResultDto>> ExecuteCoreAsync(
        McpInvocationContext context,
        CardSearchInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validationErrors = McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId");
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

        var result = await cardService.SearchCardsByTextAsync(
            boardId, new CardTextSearchRequest(input.Query, input.Offset, input.Limit), context.ActorUserId, cancellationToken);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        return Success(result.Data);
    }
}
