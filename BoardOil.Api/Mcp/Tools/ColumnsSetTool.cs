using BoardOil.Abstractions.Column;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Column;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class ColumnsSetTool(
    IColumnService columnService,
    IMcpAuthorisationService authorisationService) : McpToolBase<ColumnsSetInput, ColumnsSetOutput>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.ColumnsSet,
        "Set the board's complete column configuration.",
        ToolSchemas.ColumnsSetInput,
        ToolSchemas.ColumnsSetOutput,
        MachinePatScopes.McpWrite,
        ToolDiscoveryOrder.ColumnsSet,
        McpToolBehaviours.Destructive);

    protected override async Task<McpToolResult<ColumnsSetOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        ColumnsSetInput input,
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

        return Success(new ColumnsSetOutput(boardId,
            result.Data.Select(column => new McpCardOptionColumn(column.Id, column.Title)).ToArray(), "edited"));
    }
}
