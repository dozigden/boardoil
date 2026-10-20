using BoardOil.Abstractions.Column;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Column;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class ColumnEditTool(
    IColumnService columnService,
    IMcpAuthorisationService authorisationService) : McpToolBase<ColumnEditInput, ColumnEditOutput>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.ColumnEdit,
        "Set the board's complete column configuration.",
        ToolSchemas.ColumnEditInput,
        ToolSchemas.ColumnEditOutput,
        MachinePatScopes.McpWrite,
        ToolDiscoveryOrder.ColumnEdit,
        McpToolBehaviours.Destructive);

    protected override async Task<McpToolResult<ColumnEditOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        ColumnEditInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validationErrors = McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId");
        if (validationErrors.Count > 0)
        {
            return Failure(validationErrors);
        }

        var boardId = input.BoardId!.Value;
        var accessError = AuthorisationService.EnsureToolAccess(context.AccessContext, Definition.RequiredScope, boardId);
        if (accessError is not null)
        {
            return Failure(accessError);
        }

        var result = await columnService.EditColumnsAsync(boardId, new EditColumnsRequest(input.Columns), context.ActorUserId);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        return Success(new ColumnEditOutput(boardId,
            result.Data.Select(column => new McpCardOptionColumn(column.Id, column.Title)).ToArray(), "edited"));
    }
}
