using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Column;
using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class ColumnRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityBoardColumn>(ambientDbContextLocator), IColumnRepository
{
    public async Task<IReadOnlyList<EntityBoardColumn>> GetColumnsInBoardOrderedAsync(int boardId) =>
        await DbSet
            .Where(x => x.BoardId == boardId)
            .OrderBy(x => x.SortKey)
            .ToListAsync();

    public Task<bool> HasCardsAsync(int columnId) =>
        DbSet.AnyAsync(column => column.Id == columnId && column.Cards.Any());
}
