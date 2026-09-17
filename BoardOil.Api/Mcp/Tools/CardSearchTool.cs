using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardSearchTool(
    ICardService cardService,
    ICardArchiveService cardArchiveService,
    IMcpAuthorisationService authorisationService)
    : McpToolBase<CardSearchInput, object>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.CardSearch,
        "Search live cards by default, or set archived true to search archived cards. Live search matches card number, title, description, or external URL; archived search matches title or tag name. Matching is a case-insensitive literal substring. Returns paginated summaries; use card_get with the same archived selector for full detail.",
        ToolSchemas.CardSearchInput, ToolSchemas.CardSearchOutput, MachinePatScopes.McpRead,
        ToolDiscoveryOrder.CardSearch, McpToolBehaviours.ReadOnly);

    protected override async Task<McpToolResult<object>> ExecuteCoreAsync(
        McpInvocationContext context,
        CardSearchInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var boardIdValidationErrors = McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId");
        if (boardIdValidationErrors.Count > 0)
        {
            return Failure(boardIdValidationErrors);
        }

        var boardId = input.BoardId!.Value;
        var accessError = AuthorisationService.EnsureToolAccess(context.AccessContext, Definition.RequiredScope, boardId);
        if (accessError is not null)
        {
            return Failure(accessError);
        }

        var searchValidationErrors = ValidateSearchInput(input);
        if (searchValidationErrors.Count > 0)
        {
            return Failure(searchValidationErrors);
        }

        if (input.Archived)
        {
            var archivedResult = await cardArchiveService.GetArchivedCardsAsync(
                boardId, input.Query, input.Offset, input.Limit, context.ActorUserId);
            if (!archivedResult.Success || archivedResult.Data is null)
            {
                return Failure(archivedResult.ToMcpError());
            }

            return Success(archivedResult.Data.ToMcp());
        }

        var result = await cardService.SearchCardsByTextAsync(
            boardId, new CardTextSearchRequest(input.Query, input.Offset, input.Limit), context.ActorUserId, cancellationToken);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        return Success(result.Data);
    }

    private static IReadOnlyList<ValidationError> ValidateSearchInput(CardSearchInput input)
    {
        var validationErrors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(input.Query))
        {
            validationErrors.Add(new ValidationError("query", "A non-empty search query is required."));
        }

        if (input.Offset < 0)
        {
            validationErrors.Add(new ValidationError("offset", "Offset must be non-negative."));
        }

        if (input.Limit is < 1 or > 100)
        {
            validationErrors.Add(new ValidationError("limit", "Limit must be between 1 and 100."));
        }

        return validationErrors;
    }
}
