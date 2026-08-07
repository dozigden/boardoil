using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.Slick;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardUpdateTool(
    ICardService cardService,
    ISlickService slickService,
    IMcpAuthorisationService authorisationService) : McpToolBase<CardUpdateInput, CardMutationOutput>(authorisationService)
{
    private readonly ICardService _cardService = cardService;
    private readonly ISlickService _slickService = slickService;

    public override McpToolDefinition Definition { get; } =
        new(ToolNames.CardUpdate, "Update card title, description, tags, slick selection, external URL, and optional target column.", ToolSchemas.CardUpdateInput, ToolSchemas.ObjectOutput, MachinePatScopes.McpWrite);

    protected override async Task<McpToolResult<CardMutationOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        CardUpdateInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<ValidationError> validationErrors =
        [
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId"),
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.Id, "id"),
            ..McpToolCallHelpers.ValidateOptionalIdentifier(input.ColumnId, "columnId"),
            ..McpToolCallHelpers.ValidateOptionalIdentifier(input.AssignedUserId, "assignedUserId"),
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.CardTypeId, "cardTypeId")
        ];
        if (!input.SlickNameSpecified)
        {
            validationErrors = [..validationErrors, new ValidationError("slickName", "Slick selection is required. Provide slickName or null.")];
        }
        if (!input.ExternalUrlSpecified)
        {
            validationErrors = [..validationErrors, new ValidationError("externalUrl", "External URL is required. Provide externalUrl or null.")];
        }
        if (validationErrors.Count > 0)
        {
            return Failure(validationErrors);
        }

        var boardId = input.BoardId!.Value;
        var cardId = input.Id!.Value;

        var accessError = AuthorisationService.EnsureToolAccess(context.AccessContext, Definition.RequiredScope, boardId);
        if (accessError is not null)
        {
            return Failure(accessError);
        }

        int? assignedUserId = input.AssignedUserId;
        var existingCardResult = await _cardService.GetCardAsync(boardId, cardId, context.ActorUserId);
        if (!existingCardResult.Success || existingCardResult.Data is null)
        {
            return Failure(existingCardResult.ToMcpError());
        }

        if (!input.AssignedUserIdSpecified)
        {
            assignedUserId = existingCardResult.Data.AssignedUserId;
        }

        var request = new UpdateCardRequest(
            input.Title,
            input.Description,
            input.TagNames,
            input.CardTypeId!.Value,
            input.ColumnId,
            assignedUserId,
            input.SlickName,
            input.ExternalUrl);
        var result = await _cardService.UpdateCardAsync(boardId, cardId, request, context.ActorUserId);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        IReadOnlyDictionary<int, McpCardSlickSnapshot>? slicksById = null;
        if (result.Data.SlickId is not null)
        {
            var slicksResult = await McpSlickHelpers.LoadBoardSlicksByIdAsync(_slickService, boardId, context.ActorUserId, cancellationToken);
            if (!slicksResult.Success)
            {
                return Failure((slicksResult.Error ?? ApiErrors.InternalError("Failed to load slicks.")).ToMcpError());
            }

            slicksById = slicksResult.SlicksById;
        }

        return Success(new CardMutationOutput(result.Data.ToMcp(slicksById), "updated"));
    }
}
