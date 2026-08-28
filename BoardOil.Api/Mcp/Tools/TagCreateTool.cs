using BoardOil.Abstractions.Tag;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class TagCreateTool(
    ITagService tagService,
    IMcpAuthorisationService authorisationService) : McpToolBase<TagCreateInput, TagMutationOutput>(authorisationService)
{
    private readonly ITagService _tagService = tagService;

    public override McpToolDefinition Definition { get; } =
        new(
            ToolNames.TagCreate,
            "Create a complete tag definition. Existing names are returned without mutation.",
            ToolSchemas.TagCreateInput,
            ToolSchemas.TagCreateOutput,
            MachinePatScopes.McpWrite);

    protected override async Task<McpToolResult<TagMutationOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        TagCreateInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validationErrors = new List<ValidationError>();
        validationErrors.AddRange(McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId"));
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            validationErrors.Add(new ValidationError("name", "Tag name is required."));
        }
        else if (input.Name.Trim().Length > 40)
        {
            validationErrors.Add(new ValidationError("name", "Tag name must be 40 characters or fewer."));
        }

        if (!input.EmojiSpecified)
        {
            validationErrors.Add(new ValidationError("emoji", "Emoji is required. Use null for no emoji."));
        }

        McpTagStyleMappingResult? styleMapping = null;
        if (!input.StyleSpecified || input.Style is null)
        {
            validationErrors.Add(new ValidationError("style", "Style is required."));
        }
        else
        {
            styleMapping = McpTagStyleMapper.Parse(input.Style);
            validationErrors.AddRange(styleMapping.ValidationErrors);
        }

        if (validationErrors.Count > 0 || styleMapping is null || !styleMapping.IsValid)
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

        var definition = new TagDefinitionCreate(
            input.Name,
            input.Emoji,
            new TagStylePatch(styleMapping.StyleName, styleMapping.StylePropertiesJson));
        var createResult = await _tagService.CreateTagDefinitionAsync(
            boardId,
            definition,
            context.ActorUserId);
        if (!createResult.Success || createResult.Data is null)
        {
            return Failure(createResult.ToMcpError());
        }

        var tag = createResult.Data;
        var snapshot = McpTagStyleMapper.ToMcpSnapshot(tag);
        if (snapshot is null)
        {
            return Failure(new McpToolError(
                "data_integrity_error",
                $"Tag {tag.Id} ('{tag.Name}') has an invalid style definition.",
                500));
        }

        var outcome = createResult.StatusCode == 201 ? "created" : "existing";
        return Success(new TagMutationOutput(snapshot, outcome));
    }
}
