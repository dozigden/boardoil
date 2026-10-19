using BoardOil.Abstractions.Column;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Column;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class ColumnUpdateTool(
    IColumnService columnService,
    IMcpAuthorisationService authorisationService) : McpToolBase<ColumnUpdateInput, ColumnMutationOutput>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.ColumnUpdate,
        "Update a column.",
        ToolSchemas.ColumnUpdateInput,
        ToolSchemas.ColumnUpdateOutput,
        MachinePatScopes.McpWrite,
        ToolDiscoveryOrder.ColumnUpdate,
        McpToolBehaviours.IdempotentDestructive);

    protected override async Task<McpToolResult<ColumnMutationOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        ColumnUpdateInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validationErrors = new List<ValidationError>();
        validationErrors.AddRange(McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId"));
        validationErrors.AddRange(McpToolCallHelpers.ValidateRequiredIdentifier(input.Id, "id"));
        if (input.Title is null)
        {
            validationErrors.Add(new ValidationError("title", "Column title is required."));
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

        var result = await columnService.UpdateColumnAsync(boardId, input.Id!.Value, new UpdateColumnRequest(input.Title!), context.ActorUserId);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        return Success(new ColumnMutationOutput(result.Data.Id, "updated"));
    }
}
