using BoardOil.Abstractions.Board;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Contracts;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class BoardGetTool(
    IBoardService boardService,
    IMcpAuthorisationService authorisationService) : McpToolBase<BoardGetInput, McpBoardSnapshot>(authorisationService)
{
    private readonly IBoardService _boardService = boardService;

    public override McpToolDefinition Definition { get; } =
        new(ToolNames.BoardGet, "Get a board snapshot including columns and cards.", ToolSchemas.BoardGetInput, ToolSchemas.ObjectOutput);

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

        var accessError = AuthorisationService.EnsurePatToolAccess(context.PatAccessContext, MachinePatScopes.McpRead, boardId);
        if (accessError is not null)
        {
            return Failure(accessError);
        }

        var result = await _boardService.GetBoardAsync(boardId);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        return Success(result.Data.ToMcp());
    }
}
