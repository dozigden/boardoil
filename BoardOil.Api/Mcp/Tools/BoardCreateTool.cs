using BoardOil.Abstractions.Board;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Common;
using BoardOil.Mcp.Contracts;
using BoardOil.Mcp.Contracts.Schemas;

namespace BoardOil.Api.Mcp;

public sealed class BoardCreateTool(
    IBoardService boardService,
    IBoardCloneService boardCloneService,
    IMcpAuthorisationService authorisationService) : McpToolBase<BoardCreateInput, BoardMutationReceipt>(authorisationService)
{
    public override McpToolDefinition Definition { get; } = new(
        ToolNames.BoardCreate,
        "Create a board normally, or use clone mode to copy an accessible board's configuration without cards, archives or members. The actor owns the new board. Description is optional: cloning inherits it when omitted and an empty string clears it.",
        ToolSchemas.BoardCreateInput,
        ToolSchemas.BoardCreateOutput,
        MachinePatScopes.McpWrite,
        ToolDiscoveryOrder.BoardCreate,
        McpToolBehaviours.NonDestructive);

    protected override async Task<McpToolResult<BoardMutationReceipt>> ExecuteCoreAsync(
        McpInvocationContext context,
        BoardCreateInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validationErrors = new List<ValidationError>();
        if (input.Mode is not ("normal" or "clone"))
        {
            validationErrors.Add(new ValidationError("mode", "Mode must be normal or clone."));
        }
        if (input.Name is null)
        {
            validationErrors.Add(new ValidationError("name", "Board name is required."));
        }
        if (input.DescriptionSpecified && input.Description is null)
        {
            validationErrors.Add(new ValidationError("description", "Description must be a string when provided. Omit it to use the default or inherited description."));
        }
        if (input.Mode == "clone")
        {
            validationErrors.AddRange(McpToolCallHelpers.ValidateRequiredIdentifier(input.SourceBoardId, "sourceBoardId"));
        }
        else if (input.Mode == "normal" && input.SourceBoardIdSpecified)
        {
            validationErrors.Add(new ValidationError("sourceBoardId", "Source board ID must be omitted in normal mode."));
        }
        if (validationErrors.Count > 0)
        {
            return Failure(validationErrors);
        }

        McpToolError? accessError;
        if (input.Mode == "clone")
        {
            accessError = AuthorisationService.EnsureToolAccess(context.AccessContext, Definition.RequiredScope, input.SourceBoardId!.Value);
        }
        else
        {
            accessError = AuthorisationService.EnsureScopeAccess(context.AccessContext, Definition.RequiredScope);
        }
        if (accessError is not null)
        {
            return Failure(accessError);
        }

        ApiResult<BoardDto> result;
        if (input.Mode == "clone")
        {
            result = await boardCloneService.CloneBoardAsync(input.SourceBoardId!.Value,
                new CloneBoardRequest(input.Name!, input.Description), context.ActorUserId);
        }
        else
        {
            result = await boardService.CreateBoardAsync(new CreateBoardRequest(input.Name!, input.Description), context.ActorUserId);
        }
        if (!result.Success || result.Data is null)
        {
            return Failure(result.ToMcpError());
        }

        return Success(new BoardMutationReceipt(result.Data.Id, "created"));
    }
}
