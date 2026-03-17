using BoardOil.Abstractions.Column;
using BoardOil.Contracts.Column;
using BoardOil.Ef;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class ColumnRepository(BoardOilDbContext dbContext) : IColumnRepository
{
    public async Task<IReadOnlyList<ColumnRecord>> GetColumnsInBoardOrderedAsync(int boardId) =>
        await dbContext.Columns
            .Where(x => x.BoardId == boardId)
            .OrderBy(x => x.SortKey)
            .Select(x => new ColumnRecord(
                x.Id,
                x.BoardId,
                x.Title,
                x.SortKey,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToListAsync();

    public async Task<ColumnRecord> CreateAsync(CreateColumnRecord column)
    {
        var entity = dbContext.Columns.Add(new BoardOil.Ef.Entities.BoardColumn
        {
            BoardId = column.BoardId,
            Title = column.Title,
            SortKey = column.SortKey,
            CreatedAtUtc = column.CreatedAtUtc,
            UpdatedAtUtc = column.UpdatedAtUtc
        }).Entity;

        await dbContext.SaveChangesAsync();

        return new ColumnRecord(
            entity.Id,
            entity.BoardId,
            entity.Title,
            entity.SortKey,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);
    }

    public async Task UpdateAsync(UpdateColumnRecord column)
    {
        var entity = await dbContext.Columns.FirstOrDefaultAsync(x => x.Id == column.Id);
        if (entity is null)
        {
            return;
        }

        entity.Title = column.Title;
        entity.SortKey = column.SortKey;
        entity.UpdatedAtUtc = column.UpdatedAtUtc;
        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await dbContext.Columns.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
        {
            return;
        }

        dbContext.Columns.Remove(entity);
        await dbContext.SaveChangesAsync();
    }
}
