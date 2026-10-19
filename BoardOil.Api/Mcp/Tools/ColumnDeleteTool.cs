using BoardOil.Abstractions.Column;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class ColumnDeleteTool(
    IColumnService columnService,
    IMcpAuthorisationService authorisationService) : McpToolBase<ColumnDeleteInput, ColumnMutationOutput>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.ColumnDelete,
        "Delete an empty column.",
        ToolSchemas.ColumnDeleteInput,
        ToolSchemas.ColumnDeleteOutput,
        MachinePatScopes.McpWrite,
        ToolDiscoveryOrder.ColumnDelete,
        McpToolBehaviours.IdempotentDestructive);

    protected override async Task<McpToolResult<ColumnMutationOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        ColumnDeleteInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<ValidationError> validationErrors =
        [
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId"),
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.Id, "id")
        ];
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

        var result = await columnService.DeleteColumnAsync(boardId, input.Id!.Value, context.ActorUserId);
        if (!result.Success)
        {
            return Failure(result.ToMcpError());
        }

        return Success(new ColumnMutationOutput(input.Id!.Value, "deleted"));
    }
}
