using BoardOil.Ef.Entities;
using BoardOil.Services.Abstractions;
using BoardOil.Services.Board;
using BoardOil.Services.Contracts;
using BoardOil.Services.Ordering;

namespace BoardOil.Services.Column;

public sealed class ColumnService(
    IBoardRepository boardRepository,
    IColumnRepository columnRepository,
    IColumnValidator validator,
    IBoardEvents boardEvents) : IColumnService
{
    private readonly IBoardEvents _boardEvents = boardEvents;

    public async Task<ApiResult<IReadOnlyList<ColumnDto>>> GetColumnsAsync()
    {
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

        var column = new BoardColumn
        {
            BoardId = boardId.Value,
            Title = request.Title.Trim(),
            SortKey = sortKey!,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        columnRepository.Add(column);
        await columnRepository.SaveChangesAsync();

        var created = column.ToColumnDto(insertIndex);
        await _boardEvents.ColumnCreatedAsync(created);
        return ApiResults.Created(created);
    }

    public async Task<ApiResult<ColumnDto>> UpdateColumnAsync(int id, UpdateColumnRequest request)
    {
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

        var titleChanged = false;
        if (request.Title is not null)
        {
            var updateValidationErrors = validator.ValidateUpdate(request);
            if (updateValidationErrors.Count > 0)
            {
                return ValidationFail(updateValidationErrors);
            }

            var normalizedTitle = request.Title.Trim();
            titleChanged = target.Title != normalizedTitle;
            target.Title = normalizedTitle;
        }

        var positionChanged = targetIndex != currentIndex;
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

            target.SortKey = sortKey!;
        }

        var now = DateTime.UtcNow;
        if (titleChanged)
        {
            target.UpdatedAtUtc = now;
        }

        if (positionChanged)
        {
            target.UpdatedAtUtc = now;
            await columnRepository.SaveChangesAsync();
        }
        else if (titleChanged)
        {
            await columnRepository.SaveChangesAsync();
        }

        var dto = target.ToColumnDto(positionChanged ? targetIndex : currentIndex);
        await _boardEvents.ColumnUpdatedAsync(dto);
        return dto;
    }

    public async Task<ApiResult> DeleteColumnAsync(int id)
    {
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
        await columnRepository.SaveChangesAsync();

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
