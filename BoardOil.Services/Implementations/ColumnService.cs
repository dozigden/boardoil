using BoardOil.Ef;
using BoardOil.Ef.Entities;
using BoardOil.Services.Abstractions;
using BoardOil.Services.Contracts;
using BoardOil.Services.Mappings;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace BoardOil.Services.Implementations;

public sealed class ColumnService(BoardOilDbContext dbContext) : IColumnService
{
    private static readonly Regex AllowedColumnTitleRegex =
        new("^[A-Za-z0-9][A-Za-z0-9 \\-._&'(),!?:/]*$", RegexOptions.Compiled);

    public async Task<ApiResult<IReadOnlyList<ColumnDto>>> GetColumnsAsync(CancellationToken cancellationToken = default)
    {
        var boardId = await GetBoardIdAsync(cancellationToken);
        if (boardId is null)
        {
            return ApiErrors.InternalError("No board exists. Bootstrap has not run.");
        }

        var columns = await dbContext.Columns
            .Where(x => x.BoardId == boardId.Value)
            .OrderBy(x => x.Position)
            .Select(x => x.ToColumnDto())
            .ToListAsync(cancellationToken);

        return columns;
    }

    public async Task<ApiResult<ColumnDto>> CreateColumnAsync(CreateColumnRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeTitle(request.Title, out var title, out var titleError))
        {
            return ValidationFail(titleError!);
        }

        var boardId = await GetBoardIdAsync(cancellationToken);
        if (boardId is null)
        {
            return ApiErrors.InternalError("No board exists. Bootstrap has not run.");
        }

        var columns = await dbContext.Columns
            .Where(x => x.BoardId == boardId.Value)
            .OrderBy(x => x.Position)
            .ToListAsync(cancellationToken);

        var insertIndex = request.Position is null
            ? columns.Count
            : Math.Clamp(request.Position.Value, 0, columns.Count);

        var now = DateTime.UtcNow;
        var column = new BoardColumn
        {
            BoardId = boardId.Value,
            Title = title!,
            Position = insertIndex,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.Columns.Add(column);
        columns.Insert(insertIndex, column);

        await PersistColumnOrderAsync(columns, cancellationToken);

        return ApiResults.Created(column.ToColumnDto());
    }

    public async Task<ApiResult<ColumnDto>> UpdateColumnAsync(int id, UpdateColumnRequest request, CancellationToken cancellationToken = default)
    {
        var boardId = await GetBoardIdAsync(cancellationToken);
        if (boardId is null)
        {
            return ApiErrors.InternalError("No board exists. Bootstrap has not run.");
        }

        var columns = await dbContext.Columns
            .Where(x => x.BoardId == boardId.Value)
            .OrderBy(x => x.Position)
            .ToListAsync(cancellationToken);

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
            if (!TryNormalizeTitle(request.Title, out var title, out var titleError))
            {
                return ValidationFail(titleError!);
            }

            titleChanged = target.Title != title;
            target.Title = title!;
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
            await PersistColumnOrderAsync(columns, cancellationToken, now);
        }
        else if (titleChanged)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return target.ToColumnDto();
    }

    public async Task<ApiResult> DeleteColumnAsync(int id, CancellationToken cancellationToken = default)
    {
        var boardId = await GetBoardIdAsync(cancellationToken);
        if (boardId is null)
        {
            return ApiErrors.InternalError("No board exists. Bootstrap has not run.");
        }

        var columns = await dbContext.Columns
            .Where(x => x.BoardId == boardId.Value)
            .OrderBy(x => x.Position)
            .ToListAsync(cancellationToken);

        var target = columns.FirstOrDefault(x => x.Id == id);
        if (target is null)
        {
            return ApiErrors.NotFound("Column not found.");
        }

        dbContext.Columns.Remove(target);
        await dbContext.SaveChangesAsync(cancellationToken);

        var remaining = columns.Where(x => x.Id != id).ToList();
        await PersistColumnOrderAsync(remaining, cancellationToken, DateTime.UtcNow);

        return ApiResults.Ok();
    }

    private async Task<int?> GetBoardIdAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Boards
            .OrderBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static bool TryNormalizeTitle(string title, out string? normalizedTitle, out string? error)
    {
        var normalized = title.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalizedTitle = null;
            error = "Column title is required.";
            return false;
        }

        if (normalized.Length > 200)
        {
            normalizedTitle = null;
            error = "Column title must be 200 characters or fewer.";
            return false;
        }

        if (!AllowedColumnTitleRegex.IsMatch(normalized))
        {
            normalizedTitle = null;
            error = "Column title can only contain letters, numbers, spaces, and . , - _ & ' ( ) ! ? : /";
            return false;
        }

        normalizedTitle = normalized;
        error = null;
        return true;
    }

    private static ApiError ValidationFail(string errorMessage) =>
        ApiErrors.BadRequest(
            "Validation failed.",
            new Dictionary<string, string[]>
            {
                ["general"] = [errorMessage]
            });

    private async Task PersistColumnOrderAsync(List<BoardColumn> orderedColumns, CancellationToken cancellationToken, DateTime? touchedAt = null)
    {
        for (var index = 0; index < orderedColumns.Count; index++)
        {
            orderedColumns[index].Position = -1 - index;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        for (var index = 0; index < orderedColumns.Count; index++)
        {
            var column = orderedColumns[index];
            if (touchedAt is not null)
            {
                column.UpdatedAtUtc = touchedAt.Value;
            }

            column.Position = index;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

}
