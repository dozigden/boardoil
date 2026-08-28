using BoardOil.Abstractions.Tag;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.Tag;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class TagUpdateTool(
    ITagService tagService,
    IMcpAuthorisationService authorisationService) : McpToolBase<TagUpdateInput, TagMutationOutput>(authorisationService)
{
    private readonly ITagService _tagService = tagService;

    public override McpToolDefinition Definition { get; } =
        new(
            ToolNames.TagUpdate,
            "Update an existing tag's name, emoji, or structured style. Use card_options_get to resolve its current name.",
            ToolSchemas.TagUpdateInput,
            ToolSchemas.TagUpdateOutput,
            MachinePatScopes.McpWrite);

    protected override async Task<McpToolResult<TagMutationOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        TagUpdateInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validationErrors = new List<ValidationError>();
        validationErrors.AddRange(McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId"));
        if (string.IsNullOrWhiteSpace(input.CurrentTagName))
        {
            validationErrors.Add(new ValidationError(
                "currentTagName",
                "Current tag name is required."));
        }
        else if (input.CurrentTagName.Trim().Length > 40)
        {
            validationErrors.Add(new ValidationError(
                "currentTagName",
                "Current tag name must be 40 characters or fewer."));
        }

        if (!input.NameSpecified && !input.EmojiSpecified && !input.StyleSpecified)
        {
            validationErrors.Add(new ValidationError(
                string.Empty,
                "Provide at least one of name, emoji, or style."));
        }

        if (input.NameSpecified && input.Name is null)
        {
            validationErrors.Add(new ValidationError(
                "name",
                "Tag name cannot be null."));
        }

        McpTagStyleMappingResult? styleMapping = null;
        if (input.StyleSpecified)
        {
            if (input.Style is null)
            {
                validationErrors.Add(new ValidationError(
                    "style",
                    "Style cannot be null."));
            }
            else
            {
                styleMapping = McpTagStyleMapper.Parse(input.Style);
                validationErrors.AddRange(styleMapping.ValidationErrors);
            }
        }

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

        TagStylePatch? stylePatch = null;
        if (styleMapping is not null && styleMapping.IsValid)
        {
            stylePatch = new TagStylePatch(
                styleMapping.StyleName,
                styleMapping.StylePropertiesJson);
        }

        var patch = new TagDefinitionPatch(
            input.NameSpecified,
            input.Name,
            input.EmojiSpecified,
            input.Emoji,
            stylePatch);
        var updateResult = await _tagService.UpdateTagDefinitionAsync(
            boardId,
            input.CurrentTagName,
            patch,
            context.ActorUserId);
        if (!updateResult.Success || updateResult.Data is null)
        {
            return Failure(updateResult.ToMcpError());
        }

        var updatedTag = updateResult.Data;
        var snapshot = McpTagStyleMapper.ToMcpSnapshot(updatedTag);
        if (snapshot is null)
        {
            return Failure(new McpToolError(
                "service_error",
                "Updated tag returned an invalid style.",
                500));
        }

        return Success(new TagMutationOutput(snapshot, "updated"));
    }
}
