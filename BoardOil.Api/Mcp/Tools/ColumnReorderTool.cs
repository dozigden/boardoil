using BoardOil.Abstractions.Column;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Column;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class ColumnReorderTool(
    IColumnService columnService,
    IMcpAuthorisationService authorisationService) : McpToolBase<ColumnReorderInput, ColumnReorderOutput>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.ColumnReorder,
        "Set the complete column order.",
        ToolSchemas.ColumnReorderInput,
        ToolSchemas.ColumnReorderOutput,
        MachinePatScopes.McpWrite,
        ToolDiscoveryOrder.ColumnReorder,
        McpToolBehaviours.IdempotentNonDestructive);

    protected override async Task<McpToolResult<ColumnReorderOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        ColumnReorderInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validationErrors = new List<ValidationError>();
        validationErrors.AddRange(McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId"));
        if (input.ColumnIds is null)
        {
            validationErrors.Add(new ValidationError("columnIds", "Column IDs are required."));
        }
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

        var result = await columnService.ReorderColumnsAsync(boardId, new ReorderColumnsRequest(input.ColumnIds), context.ActorUserId);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        return Success(new ColumnReorderOutput(boardId, result.Data.Select(column => column.Id).ToArray(), "reordered"));
    }
}
