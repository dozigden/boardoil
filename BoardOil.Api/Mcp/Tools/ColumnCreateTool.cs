using BoardOil.Abstractions.Column;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Column;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class ColumnCreateTool(
    IColumnService columnService,
    IMcpAuthorisationService authorisationService) : McpToolBase<ColumnCreateInput, ColumnMutationOutput>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.ColumnCreate,
        "Add a column.",
        ToolSchemas.ColumnCreateInput,
        ToolSchemas.ColumnCreateOutput,
        MachinePatScopes.McpWrite,
        ToolDiscoveryOrder.ColumnCreate,
        McpToolBehaviours.NonDestructive);

    protected override async Task<McpToolResult<ColumnMutationOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        ColumnCreateInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validationErrors = new List<ValidationError>();
        validationErrors.AddRange(McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId"));
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

        var result = await columnService.CreateColumnAsync(boardId, new CreateColumnRequest(input.Title!), context.ActorUserId);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        return Success(new ColumnMutationOutput(result.Data.Id, "created"));
    }
}
