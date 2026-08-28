using BoardOil.Abstractions.Tag;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class TagDeleteTool(
    ITagService tagService,
    IMcpAuthorisationService authorisationService) : McpToolBase<TagDeleteInput, TagDeleteOutput>(authorisationService)
{
    private readonly ITagService _tagService = tagService;

    public override McpToolDefinition Definition { get; } =
        new(
            ToolNames.TagDelete,
            "Delete a tag and remove it from cards. Resolve its board-scoped ID from card_options_get.tags[].id.",
            ToolSchemas.TagDeleteInput,
            ToolSchemas.TagDeleteOutput,
            MachinePatScopes.McpWrite);

    protected override async Task<McpToolResult<TagDeleteOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        TagDeleteInput input,
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

        var deleteResult = await _tagService.DeleteTagAsync(
            boardId,
            input.Id!.Value,
            context.ActorUserId);
        if (!deleteResult.Success)
        {
            return Failure(deleteResult.ToMcpError());
        }

        return Success(new TagDeleteOutput("deleted"));
    }
}
