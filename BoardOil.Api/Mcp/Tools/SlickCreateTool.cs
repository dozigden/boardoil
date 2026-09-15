using BoardOil.Abstractions.Slick;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class SlickCreateTool(
    ISlickService slickService,
    IMcpAuthorisationService authorisationService) : McpToolBase<SlickCreateInput, SlickMutationOutput>(authorisationService)
{
    private readonly ISlickService _slickService = slickService;

    public override McpToolDefinition Definition { get; } =
        new(
            ToolNames.SlickCreate,
            "Create a complete reusable slick definition, including its style. Existing names are returned without mutation. To assign a slick to a card, use card_create or card_update.",
            ToolSchemas.SlickCreateInput,
            ToolSchemas.SlickCreateOutput,
            MachinePatScopes.McpWrite,
            ToolDiscoveryOrder.SlickCreate,
            McpToolBehaviours.IdempotentNonDestructive);

    protected override async Task<McpToolResult<SlickMutationOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        SlickCreateInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validationErrors = new List<ValidationError>();
        validationErrors.AddRange(McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId"));
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            validationErrors.Add(new ValidationError("name", "Slick name is required."));
        }
        else if (input.Name.Trim().Length > 40)
        {
            validationErrors.Add(new ValidationError("name", "Slick name must be 40 characters or fewer."));
        }

        McpStyleMappingResult? styleMapping = null;
        if (input.Style is null)
        {
            validationErrors.Add(new ValidationError("style", "Style is required."));
        }
        else
        {
            styleMapping = McpSlickMapper.ParseStyle(input.Style);
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

        var definition = new SlickDefinitionCreate(
            input.Name,
            new SlickStylePatch(styleMapping.StyleName, styleMapping.StylePropertiesJson));
        var createResult = await _slickService.CreateSlickDefinitionAsync(
            boardId,
            definition,
            context.ActorUserId);
        if (!createResult.Success || createResult.Data is null)
        {
            return Failure(createResult.ToMcpError());
        }

        var snapshot = McpSlickMapper.ToMcpSnapshot(createResult.Data);
        if (snapshot is null)
        {
            return Failure(new McpToolError(
                "data_integrity_error",
                $"Slick {createResult.Data.Id} ('{createResult.Data.Name}') has an invalid style definition.",
                500));
        }

        var outcome = createResult.StatusCode == 201 ? "created" : "existing";
        return Success(new SlickMutationOutput(snapshot, outcome));
    }
}
