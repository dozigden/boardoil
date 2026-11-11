using BoardOil.Abstractions.Board;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class BoardReorderTool(
    IBoardService boardService,
    IMcpAuthorisationService authorisationService) : McpToolBase<BoardReorderInput, BoardMutationReceipt>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.BoardReorder,
        "Set the complete card layout across existing columns. Read board_get first. Include every column once and every live board-scoped card number once, in desired top-to-bottom order. Empty columns require cardIds: []. Column order and card contents are preserved. The supplied order overrides slick placement. Validation and saving are atomic; invalid layouts change nothing. There is no stale-layout token: another reorder using the same card set may be overwritten.",
        ToolSchemas.BoardReorderInput,
        ToolSchemas.BoardReorderOutput,
        MachinePatScopes.McpWrite,
        ToolDiscoveryOrder.BoardReorder,
        McpToolBehaviours.IdempotentNonDestructive);

    protected override async Task<McpToolResult<BoardMutationReceipt>> ExecuteCoreAsync(
        McpInvocationContext context, BoardReorderInput input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId");
        if (errors.Count > 0)
        {
            return Failure(errors);
        }

        var boardId = input.BoardId!.Value;
        var accessError = AuthorisationService.EnsureToolAccess(context.AccessContext, Definition.RequiredScope, boardId);
        if (accessError is not null)
        {
            return Failure(accessError);
        }

        var result = await boardService.ReorderBoardAsync(boardId, new ReorderBoardRequest(input.Columns), context.ActorUserId);
        if (!result.Success)
        {
            return Failure(result.ToMcpError());
        }

        return Success(new BoardMutationReceipt(boardId, "reordered"));
    }
}
