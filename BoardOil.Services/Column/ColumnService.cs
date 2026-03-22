using BoardOil.Abstractions;
using BoardOil.Abstractions.Column;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Column;
using BoardOil.Contracts.Contracts;
using BoardOil.Persistence.Abstractions.Board;
using BoardOil.Persistence.Abstractions.Column;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Services.Ordering;

namespace BoardOil.Services.Column;

public sealed class ColumnService(
    IBoardRepository boardRepository,
    IColumnRepository columnRepository,
    IColumnValidator validator,
    IBoardEvents boardEvents,
    IDbContextScopeFactory scopeFactory) : IColumnService
{
    private readonly IBoardEvents _boardEvents = boardEvents;
    private readonly IDbContextScopeFactory _scopeFactory = scopeFactory;

    public async Task<ApiResult<IReadOnlyList<ColumnDto>>> GetColumnsAsync()
    {
        using var scope = _scopeFactory.CreateReadOnly();

        var boardId = await GetBoardIdAsync();
        if (boardId is null)
        {
            return ApiErrors.InternalError("No board exists. Bootstrap has not run.");
        }

        var columns = (await columnRepository.GetColumnsInBoardOrderedAsync(boardId.Value))
            .Select(x => x.ToColumnDto())
            .ToList();

        return columns;
    }

    public async Task<ApiResult<ColumnDto>> CreateColumnAsync(CreateColumnRequest request)
    {
        using var scope = _scopeFactory.Create();

        var validationErrors = validator.ValidateCreate(request);
        if (validationErrors.Count > 0)
        {
            return ValidationFail(validationErrors);
        }

        var boardId = await GetBoardIdAsync();
        if (boardId is null)
        {
            return ApiErrors.InternalError("No board exists. Bootstrap has not run.");
        }

        var columns = (await columnRepository.GetColumnsInBoardOrderedAsync(boardId.Value)).ToList();

        var now = DateTime.UtcNow;
        var previousKey = columns.Count > 0 ? columns[^1].SortKey : null;
        var nextKey = (string?)null;
        if (!TryGenerateSortKey(previousKey, nextKey, out var sortKey, out var allocationError))
        {
            return allocationError!;
        }

        var column = new EntityBoardColumn
        {
            BoardId = boardId.Value,
            Title = request.Title.Trim(),
            SortKey = sortKey!,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        columnRepository.Add(column);

        await scope.SaveChangesAsync();

        var created = column.ToColumnDto();
        await _boardEvents.ColumnCreatedAsync(created);
        return ApiResults.Created(created);
    }

    public async Task<ApiResult<ColumnDto>> UpdateColumnAsync(int id, UpdateColumnRequest request)
    {
        using var scope = _scopeFactory.Create();

        var boardId = await GetBoardIdAsync();
        if (boardId is null)
        {
            return ApiErrors.InternalError("No board exists. Bootstrap has not run.");
        }

        var columns = (await columnRepository.GetColumnsInBoardOrderedAsync(boardId.Value)).ToList();

        var target = columns.FirstOrDefault(x => x.Id == id);
        if (target is null)
        {
            return ApiErrors.NotFound("Column not found.");
        }

        var updatedTitle = target.Title;
        if (request.Title is not null)
        {
            var updateValidationErrors = validator.ValidateUpdate(request);
            if (updateValidationErrors.Count > 0)
            {
                return ValidationFail(updateValidationErrors);
            }

            var normalizedTitle = request.Title.Trim();
            updatedTitle = normalizedTitle;
        }
        var titleChanged = updatedTitle != target.Title;

        if (titleChanged)
        {
            target.Title = updatedTitle;
            target.UpdatedAtUtc = DateTime.UtcNow;

            await scope.SaveChangesAsync();
        }

        var dto = target.ToColumnDto();
        await _boardEvents.ColumnUpdatedAsync(dto);
        return dto;
    }

    public async Task<ApiResult<ColumnDto>> MoveColumnAsync(int id, MoveColumnRequest request)
    {
        using var scope = _scopeFactory.Create();

        var boardId = await GetBoardIdAsync();
        if (boardId is null)
        {
            return ApiErrors.InternalError("No board exists. Bootstrap has not run.");
        }

        var columns = (await columnRepository.GetColumnsInBoardOrderedAsync(boardId.Value)).ToList();
        var target = columns.FirstOrDefault(x => x.Id == id);
        if (target is null)
        {
            return ApiErrors.NotFound("Column not found.");
        }

        if (request.PositionAfterColumnId == id)
        {
            return ValidationFail([new ValidationError("positionAfterColumnId", "Column cannot be positioned after itself.")]);
        }

        var currentIndex = FindColumnIndex(columns, id);
        var currentPositionAfterColumnId = currentIndex > 0 ? columns[currentIndex - 1].Id : (int?)null;
        if (request.PositionAfterColumnId == currentPositionAfterColumnId)
        {
            var unchangedDto = target.ToColumnDto();
            await _boardEvents.ColumnUpdatedAsync(unchangedDto);
            return unchangedDto;
        }

        var targetColumns = columns
            .Where(x => x.Id != id)
            .ToList();

        var anchorResolution = ResolveAnchor(request.PositionAfterColumnId, targetColumns);
        if (anchorResolution.Error is not null)
        {
            return anchorResolution.Error;
        }

        if (!TryGenerateSortKey(
                anchorResolution.PreviousKey,
                anchorResolution.NextKey,
                out var targetSortKeyValue,
                out var allocationError))
        {
            return allocationError!;
        }

        var targetSortKey = targetSortKeyValue!;
        if (target.SortKey != targetSortKey)
        {
            target.SortKey = targetSortKey;
            target.UpdatedAtUtc = DateTime.UtcNow;
            await scope.SaveChangesAsync();
        }

        var dto = target.ToColumnDto();
        await _boardEvents.ColumnUpdatedAsync(dto);
        return dto;
    }

    public async Task<ApiResult> DeleteColumnAsync(int id)
    {
        using var scope = _scopeFactory.Create();

        var boardId = await GetBoardIdAsync();
        if (boardId is null)
        {
            return ApiErrors.InternalError("No board exists. Bootstrap has not run.");
        }

        var columns = (await columnRepository.GetColumnsInBoardOrderedAsync(boardId.Value)).ToList();

        var target = columns.FirstOrDefault(x => x.Id == id);
        if (target is null)
        {
            return ApiResults.Ok();
        }

        columnRepository.Remove(target);
        await scope.SaveChangesAsync();

        await _boardEvents.ColumnDeletedAsync(id);
        return ApiResults.Ok();
    }

    private async Task<int?> GetBoardIdAsync()
    {
        return await boardRepository.GetPrimaryBoardIdAsync();
    }

    private static ApiError ValidationFail(IReadOnlyList<ValidationError> validationErrors) =>
        ApiErrors.BadRequest("Validation failed.", validationErrors);

    private static (ApiError? Error, string? PreviousKey, string? NextKey) ResolveAnchor(
        int? positionAfterColumnId,
        IReadOnlyList<EntityBoardColumn> targetColumns)
    {
        if (positionAfterColumnId is null)
        {
            var firstSortKey = targetColumns.Count > 0 ? targetColumns[0].SortKey : null;
            return (null, null, firstSortKey);
        }

        var anchorIndex = FindColumnIndex(targetColumns, positionAfterColumnId.Value);
        if (anchorIndex < 0)
        {
            return (ValidationFail([new ValidationError("positionAfterColumnId", "Column does not exist in board.")]), null, null);
        }

        var previousKey = targetColumns[anchorIndex].SortKey;
        var nextKey = anchorIndex < targetColumns.Count - 1
            ? targetColumns[anchorIndex + 1].SortKey
            : null;
        return (null, previousKey, nextKey);
    }

    private static int FindColumnIndex(IReadOnlyList<EntityBoardColumn> columns, int targetColumnId)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            if (columns[i].Id == targetColumnId)
            {
                return i;
            }
        }

        return -1;
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
