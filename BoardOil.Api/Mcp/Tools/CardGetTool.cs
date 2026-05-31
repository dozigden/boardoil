using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.Slick;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardGetTool(
    ICardService cardService,
    ICardCommentService cardCommentService,
    ISlickService slickService,
    IMcpAuthorisationService authorisationService) : McpToolBase<CardGetInput, McpCardSnapshot>(authorisationService)
{
    private readonly ICardService _cardService = cardService;
    private readonly ICardCommentService _cardCommentService = cardCommentService;
    private readonly ISlickService _slickService = slickService;

    public override McpToolDefinition Definition { get; } =
        new(ToolNames.CardGet, "Get a card snapshot including description, tags, and comments.", ToolSchemas.CardGetInput, ToolSchemas.ObjectOutput);

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

        var commentsResult = await _cardCommentService.GetCommentsAsync(boardId, cardId, context.ActorUserId);
        if (!commentsResult.Success || commentsResult.Data is null)
        {
            return Failure(commentsResult.ToMcpError());
        }

        IReadOnlyDictionary<int, McpCardSlickSnapshot>? slicksById = null;
        if (result.Data.SlickId is not null)
        {
            var slicksResult = await McpSlickHelpers.LoadBoardSlicksByIdAsync(_slickService, boardId, context.ActorUserId, cancellationToken);
            if (!slicksResult.Success)
            {
                return Failure((slicksResult.Error ?? ApiErrors.InternalError("Failed to load slicks.")).ToMcpError());
            }

            slicksById = slicksResult.SlicksById;
        }

        var cardSnapshot = result.Data.ToMcp(slicksById) with
        {
            Comments = commentsResult.Data.Select(comment => comment.ToMcp()).ToArray()
        };

        return Success(cardSnapshot);
    }
}
