using BoardOil.Abstractions.Board;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Server.Contracts;
using BoardOil.Mcp.Server.Mapping;

namespace BoardOil.Mcp.Server.Tools;

public sealed class BoardGetToolHandler(IBoardService boardService) : IToolHandler<BoardGetInput, McpBoardSnapshot>
{
    public string ToolName => ToolNames.BoardGet;

    public async Task<McpToolResult<McpBoardSnapshot>> HandleAsync(BoardGetInput input, CancellationToken cancellationToken)
    {
        var result = await boardService.GetBoardAsync(input.BoardId);
        if (!result.Success || result.Data is null)
        {
            return result.ToMcpFailure<McpBoardSnapshot>();
        }

        return result.Data.ToMcp().ToMcpSuccess();
    }
}
