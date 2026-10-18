using BoardOil.Abstractions.CardType;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.CardType;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardTypeCreateTool(
    ICardTypeService cardTypeService,
    IMcpAuthorisationService authorisationService) : McpToolBase<CardTypeCreateInput, CardTypeMutationOutput>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.CardTypeCreate,
        "Create a card type with a name, emoji and style. Duplicate names fail.",
        ToolSchemas.CardTypeCreateInput,
        ToolSchemas.CardTypeCreateOutput,
        MachinePatScopes.McpWrite,
        ToolDiscoveryOrder.CardTypeCreate,
        McpToolBehaviours.NonDestructive);

    protected override async Task<McpToolResult<CardTypeMutationOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        CardTypeCreateInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validationErrors = new List<ValidationError>();
        validationErrors.AddRange(McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId"));
        if (!input.EmojiSpecified)
        {
            validationErrors.Add(new ValidationError("emoji", "Emoji is required. Use null for no emoji."));
        }

        var styleMapping = McpCardTypeMapper.ParseStyle(input.Style);
        validationErrors.AddRange(styleMapping.ValidationErrors);
        if (validationErrors.Count > 0 || !styleMapping.IsValid)
        {
            return Failure(validationErrors);
        }

        var boardId = input.BoardId!.Value;
        var accessError = AuthorisationService.EnsureToolAccess(context.AccessContext, Definition.RequiredScope, boardId);
        if (accessError is not null)
        {
            return Failure(accessError);
        }

        var request = new CreateCardTypeRequest(input.Name, input.Emoji, styleMapping.StyleName, styleMapping.StylePropertiesJson);
        var result = await cardTypeService.CreateCardTypeAsync(boardId, request, context.ActorUserId);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        var snapshot = McpCardTypeMapper.ToMcpSnapshot(result.Data);
        if (snapshot is null)
        {
            return Failure(McpCardTypeMapper.InvalidStyle(result.Data));
        }

        return Success(new CardTypeMutationOutput(snapshot, "created"));
    }
}
