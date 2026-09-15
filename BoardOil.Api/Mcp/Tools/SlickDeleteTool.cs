using BoardOil.Abstractions.Slick;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class SlickDeleteTool(
    ISlickService slickService,
    IMcpAuthorisationService authorisationService) : McpToolBase<SlickDeleteInput, SlickDeleteOutput>(authorisationService)
{
    private readonly ISlickService _slickService = slickService;

    public override McpToolDefinition Definition { get; } =
        new(
            ToolNames.SlickDelete,
            "Delete a slick and remove it from cards. Resolve its board-scoped ID from card_options_get.slicks[].id.",
            ToolSchemas.SlickDeleteInput,
            ToolSchemas.SlickDeleteOutput,
            MachinePatScopes.McpWrite);

    protected override async Task<McpToolResult<SlickDeleteOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        SlickDeleteInput input,
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
        var accessError = AuthorisationService.EnsureToolAccess(
            context.AccessContext,
            Definition.RequiredScope,
            boardId);
        if (accessError is not null)
        {
            return Failure(accessError);
        }

        var deleteResult = await _slickService.DeleteSlickAsync(
            boardId,
            input.Id!.Value,
            context.ActorUserId);
        if (!deleteResult.Success)
        {
            return Failure(deleteResult.ToMcpError());
        }

        return Success(new SlickDeleteOutput("deleted"));
    }
}
