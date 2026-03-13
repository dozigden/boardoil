using BoardOil.Ef;
using BoardOil.Ef.Entities;
using BoardOil.Services.Abstractions;
using BoardOil.Services.Contracts;
using BoardOil.Services.Mappings;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Services.Implementations;

public sealed class ColumnService(BoardOilDbContext dbContext, IColumnValidator validator) : IColumnService
{
    public async Task<ApiResult<IReadOnlyList<ColumnDto>>> GetColumnsAsync()
    {
        var boardId = await GetBoardIdAsync();
        if (boardId is null)
        {
            return ApiErrors.InternalError("No board exists. Bootstrap has not run.");
        }

        var columns = await dbContext.Columns
            .Where(x => x.BoardId == boardId.Value)
            .OrderBy(x => x.Position)
            .Select(x => x.ToColumnDto())
            .ToListAsync();

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

        var columns = await dbContext.Columns
            .Where(x => x.BoardId == boardId.Value)
            .OrderBy(x => x.Position)
            .ToListAsync();

        var insertIndex = request.Position is null
            ? columns.Count
            : Math.Clamp(request.Position.Value, 0, columns.Count);

        var now = DateTime.UtcNow;
        var column = new BoardColumn
        {
            BoardId = boardId.Value,
            Title = request.Title.Trim(),
            Position = insertIndex,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.Columns.Add(column);
        columns.Insert(insertIndex, column);

        await PersistColumnOrderAsync(columns);

        return ApiResults.Created(column.ToColumnDto());
    }

    public async Task<ApiResult<ColumnDto>> UpdateColumnAsync(int id, UpdateColumnRequest request)
    {
        var boardId = await GetBoardIdAsync();
        if (boardId is null)
        {
            return ApiErrors.InternalError("No board exists. Bootstrap has not run.");
        }

        var columns = await dbContext.Columns
            .Where(x => x.BoardId == boardId.Value)
            .OrderBy(x => x.Position)
            .ToListAsync();

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

        if (targetIndex != currentIndex)
        {
            columns.RemoveAt(currentIndex);
            columns.Insert(targetIndex, target);
        }

        var now = DateTime.UtcNow;
        if (titleChanged)
        {
            target.UpdatedAtUtc = now;
        }

        if (targetIndex != currentIndex)
        {
            await PersistColumnOrderAsync(columns, now);
        }
        else if (titleChanged)
        {
            await dbContext.SaveChangesAsync();
        }

        return target.ToColumnDto();
    }

    public async Task<ApiResult> DeleteColumnAsync(int id)
    {
        var boardId = await GetBoardIdAsync();
        if (boardId is null)
        {
            return ApiErrors.InternalError("No board exists. Bootstrap has not run.");
        }

        var columns = await dbContext.Columns
            .Where(x => x.BoardId == boardId.Value)
            .OrderBy(x => x.Position)
            .ToListAsync();

        var target = columns.FirstOrDefault(x => x.Id == id);
        if (target is null)
        {
            return ApiErrors.NotFound("Column not found.");
        }

        dbContext.Columns.Remove(target);
        await dbContext.SaveChangesAsync();

        var remaining = columns.Where(x => x.Id != id).ToList();
        await PersistColumnOrderAsync(remaining, DateTime.UtcNow);

        return ApiResults.Ok();
    }

    private async Task<int?> GetBoardIdAsync()
    {
        return await dbContext.Boards
            .OrderBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();
    }

    private static ApiError ValidationFail(IReadOnlyList<ValidationError> validationErrors) =>
        ApiErrors.BadRequest(
            "Validation failed.",
            validationErrors
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Property) ? string.Empty : x.Property)
                .ToDictionary(x => x.Key, x => x.Select(y => y.Message).ToArray()));

    private async Task PersistColumnOrderAsync(List<BoardColumn> orderedColumns, DateTime? touchedAt = null)
    {
        for (var index = 0; index < orderedColumns.Count; index++)
        {
            orderedColumns[index].Position = -1 - index;
        }

        await dbContext.SaveChangesAsync();

        for (var index = 0; index < orderedColumns.Count; index++)
        {
            var column = orderedColumns[index];
            if (touchedAt is not null)
            {
                column.UpdatedAtUtc = touchedAt.Value;
            }

            column.Position = index;
        }

        await dbContext.SaveChangesAsync();
    }

}
