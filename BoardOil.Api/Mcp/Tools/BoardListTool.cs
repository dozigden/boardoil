using BoardOil.Abstractions.Board;
using BoardOil.Contracts.Auth;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class BoardListTool(
    IBoardService boardService,
    IMcpAuthorisationService authorisationService) : McpToolBase<BoardListInput, BoardListOutput>(authorisationService)
{
    private readonly IBoardService _boardService = boardService;

    public override McpToolDefinition Definition { get; } =
        new(ToolNames.BoardList, "List boards accessible to the actor so clients can discover board ids before snapshot calls.", ToolSchemas.BoardListInput, ToolSchemas.ObjectOutput);

    protected override async Task<McpToolResult<BoardListOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        BoardListInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var accessError = AuthorisationService.EnsurePatScopeAccess(context.PatAccessContext, MachinePatScopes.McpRead);
        if (accessError is not null)
        {
            return Failure(accessError);
        }

        var result = await _boardService.GetBoardsAsync(context.ActorUserId);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        return Success(new BoardListOutput(result.Data.Select(board => board.ToMcp()).ToArray()));
    }
}
