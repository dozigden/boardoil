using BoardOil.Abstractions.Column;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Column;
using Microsoft.EntityFrameworkCore;
using ColumnEntity = BoardOil.Ef.Entities.BoardColumn;

namespace BoardOil.Ef.Repositories;

public sealed class ColumnRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase(ambientDbContextLocator), IColumnRepository
{
    public async Task<IReadOnlyList<ColumnRecord>> GetColumnsInBoardOrderedAsync(int boardId) =>
        await DbContext.Columns
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

    public void Add(CreateColumnRecord column)
    {
        DbContext.Columns.Add(new ColumnEntity
        {
            BoardId = column.BoardId,
            Title = column.Title,
            SortKey = column.SortKey,
            CreatedAtUtc = column.CreatedAtUtc,
            UpdatedAtUtc = column.UpdatedAtUtc
        });
    }

    public async Task UpdateAsync(UpdateColumnRecord column)
    {
        var entity = await DbContext.Columns.FirstOrDefaultAsync(x => x.Id == column.Id);
        if (entity is null)
        {
            return;
        }

        entity.Title = column.Title;
        entity.SortKey = column.SortKey;
        entity.UpdatedAtUtc = column.UpdatedAtUtc;
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await DbContext.Columns.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
        {
            return;
        }

        DbContext.Columns.Remove(entity);
    }
}
