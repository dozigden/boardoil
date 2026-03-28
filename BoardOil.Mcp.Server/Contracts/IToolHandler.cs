using BoardOil.Mcp.Contracts;

namespace BoardOil.Mcp.Server.Contracts;

public interface IToolHandler<in TInput, TOutput>
{
    string ToolName { get; }

    Task<McpToolResult<TOutput>> HandleAsync(TInput input, CancellationToken cancellationToken);
}
