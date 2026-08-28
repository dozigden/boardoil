using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;
using BoardOil.Services.Style;

namespace BoardOil.Api.Mcp;

public sealed class CardOptionsGetTool(
    ICardOptionsService cardOptionsService,
    IMcpAuthorisationService authorisationService) : McpToolBase<CardOptionsGetInput, CardOptionsGetOutput>(authorisationService)
{
    private readonly ICardOptionsService _cardOptionsService = cardOptionsService;

    public override McpToolDefinition Definition { get; } =
        new(
            ToolNames.CardOptionsGet,
            "List board-scoped values used by card fields: columns, active assignees, card types, complete tag definitions, and slicks.",
            ToolSchemas.CardOptionsGetInput,
            ToolSchemas.CardOptionsGetOutput,
            MachinePatScopes.McpRead);

    protected override async Task<McpToolResult<CardOptionsGetOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        CardOptionsGetInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<ValidationError> validationErrors =
        [
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.Id, "id")
        ];
        if (validationErrors.Count > 0)
        {
            return Failure(validationErrors);
        }

        var boardId = input.Id!.Value;
        var accessError = AuthorisationService.EnsureToolAccess(context.AccessContext, Definition.RequiredScope, boardId);
        if (accessError is not null)
        {
            return Failure(accessError);
        }

        var result = await _cardOptionsService.GetOptionsAsync(boardId, context.ActorUserId);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        var tags = new List<McpCardOptionTag>(result.Data.Tags.Count);
        foreach (var tag in result.Data.Tags)
        {
            var parsedStyle = StyleDefinitionCodec.ParseCompatible(
                tag.StyleName,
                tag.StylePropertiesJson);
            if (!parsedStyle.IsValid || parsedStyle.Definition is null)
            {
                return Failure(new McpToolError(
                    "data_integrity_error",
                    $"Tag {tag.Id} ('{tag.Name}') has an invalid style definition.",
                    500));
            }

            tags.Add(new McpCardOptionTag(
                tag.Id,
                tag.Name,
                tag.Emoji,
                McpTagStyleMapper.ToMcp(parsedStyle.Definition)));
        }

        return Success(new CardOptionsGetOutput(
            result.Data.Id,
            result.Data.Columns.Select(column => new McpCardOptionColumn(column.Id, column.Title)).ToArray(),
            result.Data.Members.Select(member => new McpCardOptionMember(
                member.UserId,
                member.UserName,
                member.DisplayName,
                member.Role)).ToArray(),
            result.Data.CardTypes.Select(cardType => new McpCardOptionCardType(
                cardType.Id,
                cardType.Name,
                cardType.Emoji)).ToArray(),
            result.Data.DefaultCardTypeId,
            tags,
            result.Data.Slicks.Select(slick => new McpCardOptionSlick(slick.Name)).ToArray()));
    }
}
