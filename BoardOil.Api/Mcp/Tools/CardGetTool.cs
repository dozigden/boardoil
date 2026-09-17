using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.Attachment;
using BoardOil.Abstractions.Slick;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardGetTool(
    ICardService cardService,
    ICardArchiveService cardArchiveService,
    ICardCommentService cardCommentService,
    ICardAttachmentService attachmentService,
    ISlickService slickService,
    IMcpAuthorisationService authorisationService) : McpToolBase<CardGetInput, object>(authorisationService)
{
    private readonly ICardService _cardService = cardService;
    private readonly ICardCommentService _cardCommentService = cardCommentService;
    private readonly ISlickService _slickService = slickService;

    public override McpToolDefinition Definition { get; } =
        new(ToolNames.CardGet, "Get a live card snapshot by default, or set archived true to inspect an archived card. Includes description, tags, comments, and attachment metadata (not file contents). Archived comments do not expose live comment ids.", ToolSchemas.CardGetInput, ToolSchemas.CardGetOutput, MachinePatScopes.McpRead, ToolDiscoveryOrder.CardGet, McpToolBehaviours.ReadOnly);

    protected override async Task<McpToolResult<object>> ExecuteCoreAsync(
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

        var accessError = AuthorisationService.EnsureToolAccess(context.AccessContext, Definition.RequiredScope, boardId);
        if (accessError is not null)
        {
            return Failure(accessError);
        }

        if (input.Archived)
        {
            return await GetArchivedCardAsync(context, boardId, cardId, cancellationToken);
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

        var attachmentsResult = await attachmentService.ListAsync(boardId, cardId, false, context.ActorUserId);
        if (!attachmentsResult.Success || attachmentsResult.Data is null)
        {
            return Failure(attachmentsResult.ToMcpError());
        }

        var cardSnapshot = result.Data.ToMcp(slicksById) with
        {
            Comments = commentsResult.Data.Select(comment => comment.ToMcp()).ToArray(),
            Attachments = attachmentsResult.Data.Items
        };

        return Success(cardSnapshot);
    }

    private async Task<McpToolResult<object>> GetArchivedCardAsync(
        McpInvocationContext context,
        int boardId,
        int cardId,
        CancellationToken cancellationToken)
    {
        var result = await cardArchiveService.GetArchivedCardAsync(boardId, cardId, context.ActorUserId);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        IReadOnlyDictionary<int, McpCardSlickSnapshot>? slicksById = null;
        if (result.Data.Card.SlickId is not null)
        {
            var slicksResult = await McpSlickHelpers.LoadBoardSlicksByIdAsync(
                _slickService, boardId, context.ActorUserId, cancellationToken);
            if (!slicksResult.Success)
            {
                return Failure((slicksResult.Error ?? ApiErrors.InternalError("Failed to load slicks.")).ToMcpError());
            }

            slicksById = slicksResult.SlicksById;
        }

        var attachmentsResult = await attachmentService.ListAsync(boardId, cardId, true, context.ActorUserId);
        if (!attachmentsResult.Success || attachmentsResult.Data is null)
        {
            return Failure(attachmentsResult.ToMcpError());
        }

        return Success(result.Data.ToMcp(attachmentsResult.Data.Items, slicksById));
    }
}
