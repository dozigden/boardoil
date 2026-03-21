using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Column;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Column;
using BoardOil.Contracts.Contracts;
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
            .Select((x, index) => x.ToColumnDto(index))
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

        var insertIndex = request.Position is null
            ? columns.Count
            : Math.Clamp(request.Position.Value, 0, columns.Count);

        var now = DateTime.UtcNow;
        var previousKey = insertIndex > 0 ? columns[insertIndex - 1].SortKey : null;
        var nextKey = insertIndex < columns.Count ? columns[insertIndex].SortKey : null;
        if (!TryGenerateSortKey(previousKey, nextKey, out var sortKey, out var allocationError))
        {
            return allocationError!;
        }

        columnRepository.Add(new CreateColumnRecord(
            BoardId: boardId.Value,
            Title: request.Title.Trim(),
            SortKey: sortKey!,
            CreatedAtUtc: now,
            UpdatedAtUtc: now));

        await scope.SaveChangesAsync();

        var columnsAfterCreate = (await columnRepository.GetColumnsInBoardOrderedAsync(boardId.Value)).ToList();
        var createdColumn = columnsAfterCreate.FirstOrDefault(x => x.SortKey == sortKey);
        if (createdColumn is null)
        {
            return ApiErrors.InternalError("Created column could not be reloaded.");
        }

        var createdIndex = columnsAfterCreate.FindIndex(x => x.Id == createdColumn.Id);
        var created = createdColumn.ToColumnDto(createdIndex < 0 ? insertIndex : createdIndex);
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

        var currentIndex = columns.FindIndex(x => x.Id == id);
        var targetIndex = request.Position is null
            ? currentIndex
            : Math.Clamp(request.Position.Value, 0, columns.Count - 1);

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

        var positionChanged = targetIndex != currentIndex;
        var updatedSortKey = target.SortKey;
        if (positionChanged)
        {
            columns.RemoveAt(currentIndex);
            columns.Insert(targetIndex, target);

            var previousKey = targetIndex > 0 ? columns[targetIndex - 1].SortKey : null;
            var nextKey = targetIndex < columns.Count - 1 ? columns[targetIndex + 1].SortKey : null;
            if (!TryGenerateSortKey(previousKey, nextKey, out var sortKey, out var allocationError))
            {
                return allocationError!;
            }

            updatedSortKey = sortKey!;
        }

        var changed = titleChanged || positionChanged;
        var updated = target with
        {
            Title = updatedTitle,
            SortKey = updatedSortKey,
            UpdatedAtUtc = changed ? DateTime.UtcNow : target.UpdatedAtUtc
        };

        if (changed)
        {
            await columnRepository.UpdateAsync(new UpdateColumnRecord(
                Id: updated.Id,
                Title: updated.Title,
                SortKey: updated.SortKey,
                UpdatedAtUtc: updated.UpdatedAtUtc));

            await scope.SaveChangesAsync();
        }

        var dto = updated.ToColumnDto(positionChanged ? targetIndex : currentIndex);
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

        await columnRepository.DeleteAsync(id);
        await scope.SaveChangesAsync();

        await _boardEvents.ColumnDeletedAsync(id);
        return ApiResults.Ok();
    }

    private async Task<int?> GetBoardIdAsync()
    {
        return await boardRepository.GetPrimaryBoardIdAsync();
    }

    private static ApiError ValidationFail(IReadOnlyList<ValidationError> validationErrors) =>
        ApiErrors.BadRequest(
            "Validation failed.",
            validationErrors
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Property) ? string.Empty : x.Property)
                .ToDictionary(x => x.Key, x => x.Select(y => y.Message).ToArray()));

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
