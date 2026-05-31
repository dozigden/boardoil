using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardCommentCreateTool(
    ICardCommentService cardCommentService,
    IMcpAuthorisationService authorisationService) : McpToolBase<CardCommentCreateInput, CardCommentMutationOutput>(authorisationService)
{
    private readonly ICardCommentService _cardCommentService = cardCommentService;

    public override McpToolDefinition Definition { get; } =
        new(ToolNames.CardCommentCreate, "Add a comment to a card.", ToolSchemas.CardCommentCreateInput, ToolSchemas.ObjectOutput);

    protected override async Task<McpToolResult<CardCommentMutationOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        CardCommentCreateInput input,
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

        var accessError = AuthorisationService.EnsurePatToolAccess(context.PatAccessContext, MachinePatScopes.McpWrite, boardId);
        if (accessError is not null)
        {
            return Failure(accessError);
        }

        var result = await _cardCommentService.CreateCommentAsync(
            boardId,
            cardId,
            new CreateCardCommentRequest(input.Text),
            context.ActorUserId);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        return Success(new CardCommentMutationOutput(result.Data.ToMcp(), "created"));
    }
}
