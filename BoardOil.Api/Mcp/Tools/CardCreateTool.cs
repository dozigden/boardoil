using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.Slick;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class CardCreateTool(
    ICardService cardService,
    ISlickService slickService,
    IMcpAuthorisationService authorisationService) : McpToolBase<CardCreateInput, CardMutationOutput>(authorisationService)
{
    private readonly ICardService _cardService = cardService;
    private readonly ISlickService _slickService = slickService;

    public override McpToolDefinition Definition { get; } =
        new(ToolNames.CardCreate, "Create a card in a specific column.", ToolSchemas.CardCreateInput, ToolSchemas.ObjectOutput);

    protected override async Task<McpToolResult<CardMutationOutput>> ExecuteCoreAsync(
        McpInvocationContext context,
        CardCreateInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<ValidationError> validationErrors =
        [
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId"),
            ..McpToolCallHelpers.ValidateRequiredIdentifier(input.ColumnId, "columnId"),
            ..McpToolCallHelpers.ValidateOptionalIdentifier(input.AssignedUserId, "assignedUserId")
        ];
        if (validationErrors.Count > 0)
        {
            return Failure(validationErrors);
        }

        var boardId = input.BoardId!.Value;
        var columnId = input.ColumnId!.Value;

        var accessError = AuthorisationService.EnsurePatToolAccess(context.PatAccessContext, MachinePatScopes.McpWrite, boardId);
        if (accessError is not null)
        {
            return Failure(accessError);
        }

        var request = new CreateCardRequest(
            columnId,
            input.Title,
            input.Description,
            input.TagNames,
            input.CardTypeId,
            input.AssignedUserId,
            input.SlickName);
        var result = await _cardService.CreateCardAsync(boardId, request, context.ActorUserId);
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

        return Success(new CardMutationOutput(result.Data.ToMcp(slicksById), "created"));
    }
}
