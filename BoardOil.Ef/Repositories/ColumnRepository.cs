using BoardOil.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Column;
using BoardOil.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class ColumnRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityBoardColumn>(ambientDbContextLocator), IColumnRepository
{
    public Task<EntityBoardColumn?> GetByIdAsync(int id) =>
        DbSet.FirstOrDefaultAsync(x => x.Id == id);

    public async Task<IReadOnlyList<EntityBoardColumn>> GetColumnsInBoardOrderedAsync(int boardId) =>
        await DbSet
            .Where(x => x.BoardId == boardId)
            .OrderBy(x => x.SortKey)
            .ToListAsync();
}
