using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Column;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Column;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Data.Abstractions.Column;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Ordering;

namespace BoardOil.Services.Column;

public sealed class ColumnService(
    IBoardRepository boardRepository,
    IColumnRepository columnRepository,
    IBoardAuthorisationService boardAuthorisationService,
    IColumnValidator validator,
    IBoardEvents boardEvents,
    IDbContextScopeFactory scopeFactory) : IColumnService
{
    private readonly IBoardEvents _boardEvents = boardEvents;
    private readonly IDbContextScopeFactory _scopeFactory = scopeFactory;

    public async Task<ApiResult<IReadOnlyList<ColumnDto>>> GetColumnsAsync(int boardId, int actorUserId)
    {
        using var scope = _scopeFactory.CreateReadOnly();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardAccess);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have access to this board.");
        }

        var columns = (await columnRepository.GetColumnsInBoardOrderedAsync(boardId))
            .Select(x => x.ToColumnDto())
            .ToList();

        return columns;
    }

    public async Task<ApiResult<ColumnDto>> CreateColumnAsync(int boardId, CreateColumnRequest request, int actorUserId)
    {
        using var scope = _scopeFactory.Create();

        var validationErrors = validator.ValidateCreate(request);
        if (validationErrors.Count > 0)
        {
            return ApiErrors.ValidationFailed(validationErrors);
        }

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.ColumnManage);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var columns = (await columnRepository.GetColumnsInBoardOrderedAsync(boardId)).ToList();

        var now = DateTime.UtcNow;
        var previousKey = columns.Count > 0 ? columns[^1].SortKey : null;
        var nextKey = (string?)null;
        if (!TryGenerateSortKey(previousKey, nextKey, out var sortKey, out var allocationError))
        {
            return allocationError!;
        }

        var column = new EntityBoardColumn
        {
            BoardId = boardId,
            Title = request.Title.Trim(),
            SortKey = sortKey!,
        };
        columnRepository.Add(column);

        await scope.SaveChangesAsync();

        var created = column.ToColumnDto();
        await _boardEvents.ColumnCreatedAsync(boardId, created);
        return ApiResults.Created(created);
    }

    public async Task<ApiResult<ColumnDto>> UpdateColumnAsync(int boardId, int id, UpdateColumnRequest request, int actorUserId)
    {
        using var scope = _scopeFactory.Create();

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.ColumnManage);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var columns = (await columnRepository.GetColumnsInBoardOrderedAsync(boardId)).ToList();

        var target = columns.FirstOrDefault(x => x.Id == id);
        if (target is null)
        {
            return ApiErrors.NotFound("Column not found.");
        }

        var updateValidationErrors = validator.ValidateUpdate(request);
        if (updateValidationErrors.Count > 0)
        {
            return ApiErrors.ValidationFailed(updateValidationErrors);
        }

        var updatedTitle = request.Title.Trim();
        var titleChanged = updatedTitle != target.Title;

        if (titleChanged)
        {
            target.Title = updatedTitle;

            await scope.SaveChangesAsync();
        }

        var dto = target.ToColumnDto();
        await _boardEvents.ColumnUpdatedAsync(boardId, dto);
        return dto;
    }

    public async Task<ApiResult<IReadOnlyList<ColumnDto>>> ReorderColumnsAsync(
        int boardId, ReorderColumnsRequest request, int actorUserId)
    {
        using var scope = _scopeFactory.CreateWithTransaction(System.Data.IsolationLevel.Serializable);

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.ColumnManage);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        if (request.ColumnIds is null)
        {
            return ApiErrors.ValidationFailed([new ValidationError("columnIds", "Column IDs are required.")]);
        }

        var columns = await columnRepository.GetColumnsInBoardOrderedAsync(boardId);
        var columnsById = columns.ToDictionary(column => column.Id);
        var requestedIds = request.ColumnIds;
        if (requestedIds.Count != columns.Count
            || requestedIds.Distinct().Count() != requestedIds.Count
            || requestedIds.Any(id => !columnsById.ContainsKey(id)))
        {
            const string message = "Include every current column exactly once. Refresh the board and try again.";
            return ApiErrors.BadRequest(message, [new ValidationError("columnIds", message)]);
        }

        if (columns.Select(column => column.Id).SequenceEqual(requestedIds))
        {
            return ApiResults.Ok<IReadOnlyList<ColumnDto>>(columns.Select(column => column.ToColumnDto()).ToArray());
        }

        // Excluding the old keys avoids transient collisions with the unique board/order index.
        var keys = SortKeyGenerator.CreateEvenlySpaced(columns.Count, columns.Select(column => column.SortKey).ToArray());
        var orderedColumns = requestedIds.Select(id => columnsById[id]).ToArray();
        for (var index = 0; index < orderedColumns.Length; index++)
        {
            orderedColumns[index].SortKey = keys[index];
        }

        await scope.SaveChangesAsync();

        var result = orderedColumns.Select(column => column.ToColumnDto()).ToArray();
        await _boardEvents.ColumnsReorderedAsync(boardId, result);
        return ApiResults.Ok<IReadOnlyList<ColumnDto>>(result);
    }

    public async Task<ApiResult> DeleteColumnAsync(int boardId, int id, int actorUserId)
    {
        using var scope = _scopeFactory.CreateWithTransaction(System.Data.IsolationLevel.Serializable);

        if (boardRepository.Get(boardId) is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.ColumnManage);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var target = columnRepository.Get(id);
        if (target is null)
        {
            return ApiResults.Ok();
        }

        if (target.BoardId != boardId)
        {
            return ApiErrors.NotFound("Column not found.");
        }

        if (await columnRepository.HasCardsAsync(target.Id))
        {
            const string message = "Column must be empty before it can be deleted.";
            return ApiErrors.BadRequest(message, [new ValidationError("id", message)]);
        }

        columnRepository.Remove(target);
        await scope.SaveChangesAsync();

        await _boardEvents.ColumnDeletedAsync(boardId, id);
        return ApiResults.Ok();
    }

    private static bool TryGenerateSortKey(string? previous, string? next, out string? sortKey, out ApiError? error)
    {
        try
        {
            sortKey = SortKeyGenerator.Between(previous, next);
            error = null;
            return true;
        }
        catch (InvalidOperationException)
        {
            sortKey = null;
            error = ApiErrors.InternalError("Unable to assign column order key.");
            return false;
        }
        catch (ArgumentException)
        {
            sortKey = null;
            error = ApiErrors.InternalError("Unable to assign column order key.");
            return false;
        }
    }
}
