using BoardOil.Abstractions.Board;
using BoardOil.Contracts.Auth;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class BoardDeleteTool(
    IBoardService boardService,
    IMcpAuthorisationService authorisationService) : McpToolBase<BoardDeleteInput, BoardMutationReceipt>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.BoardDelete,
        "Delete a board. Use only when the user explicitly asks to delete it.",
        ToolSchemas.BoardDeleteInput,
        ToolSchemas.BoardDeleteOutput,
        MachinePatScopes.McpWrite,
        ToolDiscoveryOrder.BoardDelete,
        McpToolBehaviours.IdempotentDestructive);

    protected override async Task<McpToolResult<BoardMutationReceipt>> ExecuteCoreAsync(
        McpInvocationContext context, BoardDeleteInput input, CancellationToken cancellationToken)
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

        var result = await boardService.DeleteBoardAsync(boardId, context.ActorUserId);
        if (!result.Success)
        {
            return Failure(result.ToMcpError());
        }

        return Success(new BoardMutationReceipt(boardId, "deleted"));
    }
}
