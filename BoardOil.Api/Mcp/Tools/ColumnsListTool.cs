using BoardOil.Abstractions.Column;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Contracts;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class ColumnsListTool(
    IColumnService columnService,
    IMcpAuthorisationService authorisationService) : McpToolBase<ColumnsListInput, ColumnsListOutput>(authorisationService)
{
    private readonly IColumnService _columnService = columnService;

    public override McpToolDefinition Definition { get; } =
        new(ToolNames.ColumnsList, "List columns for a board so an agent can resolve dynamic states.", ToolSchemas.ColumnsListInput, ToolSchemas.ObjectOutput);

    protected override async Task<McpToolResult<ColumnsListOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        ColumnsListInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<ValidationError> validationErrors =
        [
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.Id, "id")
        ];
        if (validationErrors.Count > 0)
        {
            return Failure(validationErrors);
        }

        var boardId = input.Id!.Value;

        var accessError = AuthorisationService.EnsurePatToolAccess(context.PatAccessContext, MachinePatScopes.McpRead, boardId);
        if (accessError is not null)
        {
            return Failure(accessError);
        }

        var result = await _columnService.GetColumnsAsync(boardId);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        var output = new ColumnsListOutput(
            boardId,
            result.Data
                .Select(column => new McpColumnReference(column.Id, column.Title, column.SortKey))
                .ToArray());

        return Success(output);
    }
}
