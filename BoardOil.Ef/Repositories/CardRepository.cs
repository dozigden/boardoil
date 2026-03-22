using BoardOil.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Card;
using BoardOil.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class CardRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityBoardCard>(ambientDbContextLocator), ICardRepository
{
    public Task<EntityBoardCard?> GetByIdAsync(int id) =>
        DbSet
            .Include(x => x.CardTags)
            .FirstOrDefaultAsync(x => x.Id == id);

    public Task<bool> ColumnExistsAsync(int columnId) =>
        DbContext.Columns.AnyAsync(x => x.Id == columnId);

    public async Task<IReadOnlyList<EntityBoardCard>> GetCardsInColumnOrderedAsync(int columnId) =>
        await DbSet
            .Where(x => x.BoardColumnId == columnId)
            .OrderBy(x => x.SortKey)
            .Include(x => x.CardTags)
            .ToListAsync();

    public async Task<IReadOnlyList<EntityBoardCard>> GetCardsForColumnsOrderedAsync(IReadOnlyList<int> columnIds)
    {
        if (columnIds.Count == 0)
        {
            return Array.Empty<EntityBoardCard>();
        }

        return await DbSet
            .Where(x => columnIds.Contains(x.BoardColumnId))
            .OrderBy(x => x.SortKey)
            .Include(x => x.CardTags)
            .ToListAsync();
    }

}
