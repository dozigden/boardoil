using BoardOil.Abstractions.Slick;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class SlickUpdateTool(
    ISlickService slickService,
    IMcpAuthorisationService authorisationService) : McpToolBase<SlickUpdateInput, SlickMutationOutput>(authorisationService)
{
    private readonly ISlickService _slickService = slickService;

    public override McpToolDefinition Definition { get; } =
        new(
            ToolNames.SlickUpdate,
            "Update an existing slick's name or structured style. Resolve its board-scoped ID from card_options_get.slicks[].id.",
            ToolSchemas.SlickUpdateInput,
            ToolSchemas.SlickUpdateOutput,
            MachinePatScopes.McpWrite);

    protected override async Task<McpToolResult<SlickMutationOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        SlickUpdateInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validationErrors = new List<ValidationError>();
        validationErrors.AddRange(McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId"));
        validationErrors.AddRange(McpToolCallHelpers.ValidateRequiredIdentifier(input.Id, "id"));
        if (!input.NameSpecified && !input.StyleSpecified)
        {
            validationErrors.Add(new ValidationError(string.Empty, "Provide at least one of name or style."));
        }

        if (input.NameSpecified)
        {
            if (string.IsNullOrWhiteSpace(input.Name))
            {
                validationErrors.Add(new ValidationError("name", "Slick name is required."));
            }
            else if (input.Name.Trim().Length > 40)
            {
                validationErrors.Add(new ValidationError("name", "Slick name must be 40 characters or fewer."));
            }
        }

        McpStyleMappingResult? styleMapping = null;
        if (input.StyleSpecified)
        {
            if (input.Style is null)
            {
                validationErrors.Add(new ValidationError("style", "Style cannot be null."));
            }
            else
            {
                styleMapping = McpSlickMapper.ParseStyle(input.Style);
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

        SlickStylePatch? stylePatch = null;
        if (styleMapping is not null && styleMapping.IsValid)
        {
            stylePatch = new SlickStylePatch(
                styleMapping.StyleName,
                styleMapping.StylePropertiesJson);
        }

        var patch = new SlickDefinitionPatch(
            input.NameSpecified,
            input.Name,
            stylePatch);
        var updateResult = await _slickService.UpdateSlickDefinitionAsync(
            boardId,
            input.Id!.Value,
            patch,
            context.ActorUserId);
        if (!updateResult.Success || updateResult.Data is null)
        {
            return Failure(updateResult.ToMcpError());
        }

        var snapshot = McpSlickMapper.ToMcpSnapshot(updateResult.Data);
        if (snapshot is null)
        {
            return Failure(new McpToolError(
                "data_integrity_error",
                $"Slick {updateResult.Data.Id} ('{updateResult.Data.Name}') has an invalid style definition.",
                500));
        }

        return Success(new SlickMutationOutput(snapshot, "updated"));
    }
}
