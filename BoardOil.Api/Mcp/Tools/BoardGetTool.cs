using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Slick;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class BoardGetTool(
    IBoardService boardService,
    ISlickService slickService,
    IMcpAuthorisationService authorisationService) : McpToolBase<BoardGetInput, McpBoardSnapshot>(authorisationService)
{
    private readonly IBoardService _boardService = boardService;
    private readonly ISlickService _slickService = slickService;

    public override McpToolDefinition Definition { get; } =
        new(ToolNames.BoardGet, "Get a board snapshot including columns and cards. Card descriptions are omitted; use card_get for full text.", ToolSchemas.BoardGetInput, ToolSchemas.ObjectOutput, MachinePatScopes.McpRead);

    protected override async Task<McpToolResult<McpBoardSnapshot>> ExecuteCoreAsync(
        McpInvocationContext context,
        BoardGetInput input,
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

        var result = await _boardService.GetBoardAsync(boardId, context.ActorUserId);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        var slicksResult = await McpSlickHelpers.LoadBoardSlicksByIdAsync(_slickService, boardId, context.ActorUserId, cancellationToken);
        if (!slicksResult.Success)
        {
            return Failure((slicksResult.Error ?? ApiErrors.InternalError("Failed to load slicks.")).ToMcpError());
        }

        return Success(result.Data.ToMcp(slicksResult.SlicksById));
    }
}
