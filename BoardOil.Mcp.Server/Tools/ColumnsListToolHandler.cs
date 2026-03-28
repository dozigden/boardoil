using BoardOil.Abstractions.Column;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Server.Contracts;
using BoardOil.Mcp.Server.Mapping;

namespace BoardOil.Mcp.Server.Tools;

public sealed class ColumnsListToolHandler(IColumnService columnService) : IToolHandler<ColumnsListInput, ColumnsListOutput>
{
    public string ToolName => ToolNames.ColumnsList;

    public async Task<McpToolResult<ColumnsListOutput>> HandleAsync(ColumnsListInput input, CancellationToken cancellationToken)
    {
        var result = await columnService.GetColumnsAsync(input.BoardId);
        if (!result.Success || result.Data is null)
        {
            return result.ToMcpFailure<ColumnsListOutput>();
        }

        var output = new ColumnsListOutput(
            input.BoardId,
            result.Data
                .Select(column => new McpColumnReference(column.Id, column.Title, column.SortKey))
                .ToArray());

        return output.ToMcpSuccess();
    }
}
