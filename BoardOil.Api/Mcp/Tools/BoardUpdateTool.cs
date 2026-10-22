using BoardOil.Abstractions.Board;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class BoardUpdateTool(
    IBoardService boardService,
    IMcpAuthorisationService authorisationService) : McpToolBase<BoardUpdateInput, BoardMutationReceipt>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.BoardUpdate,
        "Replace a board's name, description and settings. Read board_get first and provide every editable field, including both settings. Use an empty description to clear it.",
        ToolSchemas.BoardUpdateInput,
        ToolSchemas.BoardUpdateOutput,
        MachinePatScopes.McpWrite,
        ToolDiscoveryOrder.BoardUpdate,
        McpToolBehaviours.IdempotentDestructive);

    protected override async Task<McpToolResult<BoardMutationReceipt>> ExecuteCoreAsync(
        McpInvocationContext context,
        BoardUpdateInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validationErrors = new List<ValidationError>();
        validationErrors.AddRange(McpToolCallHelpers.ValidateRequiredIdentifier(input.BoardId, "boardId"));
        if (input.Name is null)
        {
            validationErrors.Add(new ValidationError("name", "Board name is required."));
        }
        if (input.Description is null)
        {
            validationErrors.Add(new ValidationError("description", "Description is required. Use an empty string to clear it."));
        }
        if (input.Settings is null)
        {
            validationErrors.Add(new ValidationError("settings", "Board settings are required."));
        }
        else
        {
            if (input.Settings.SlickCohesionModeEnabled is null)
            {
                validationErrors.Add(new ValidationError("settings.slickCohesionModeEnabled", "Slick cohesion mode setting is required."));
            }
            if (input.Settings.CardAttachmentThumbnailsEnabled is null)
            {
                validationErrors.Add(new ValidationError("settings.cardAttachmentThumbnailsEnabled", "Card attachment thumbnails setting is required."));
            }
        }
        if (validationErrors.Count > 0)
        {
            return Failure(validationErrors);
        }

        var boardId = input.BoardId!.Value;
        var accessError = AuthorisationService.EnsureToolAccess(context.AccessContext, Definition.RequiredScope, boardId);
        if (accessError is not null)
        {
            return Failure(accessError);
        }

        var settings = input.Settings!;
        var request = new UpdateBoardRequest(input.Name!, settings.SlickCohesionModeEnabled!.Value,
            input.Description!, settings.CardAttachmentThumbnailsEnabled!.Value);
        var result = await boardService.UpdateBoardAsync(boardId, request, context.ActorUserId);
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        return Success(new BoardMutationReceipt(result.Data.Id, "updated"));
    }
}
