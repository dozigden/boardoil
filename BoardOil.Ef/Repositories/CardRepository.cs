using BoardOil.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Card;
using BoardOil.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class CardRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityBoardCard>(ambientDbContextLocator), ICardRepository
{
    public async Task<EntityBoardCard?> GetWithTagsByIdAsync(int id)
    {
        var card = Get(id);
        if (card is null)
        {
            return null;
        }

        await DbContext.Entry(card)
            .Reference(x => x.CardType)
            .LoadAsync();
        await DbContext.Entry(card)
            .Collection(x => x.CardTags)
            .Query()
            .Include(x => x.Tag)
            .LoadAsync();
        return card;
    }

    public Task<EntityBoardCard?> GetWithTagsAndBoardAsync(int id) =>
        DbSet
            .Include(x => x.CardType)
            .Include(x => x.CardTags)
                .ThenInclude(x => x.Tag)
            .Include(x => x.BoardColumn)
            .FirstOrDefaultAsync(x => x.Id == id);

    public async Task<IReadOnlyList<EntityBoardCard>> GetByBoardAndCardTypeAsync(int boardId, int cardTypeId) =>
        await DbSet
            .Where(x => x.CardTypeId == cardTypeId && x.BoardColumn.BoardId == boardId)
            .ToListAsync();

    public Task<bool> ColumnExistsAsync(int columnId) =>
        DbContext.Columns.AnyAsync(x => x.Id == columnId);

    public async Task<IReadOnlyList<EntityBoardCard>> GetCardsInColumnOrderedAsync(int columnId) =>
        await DbSet
            .Where(x => x.BoardColumnId == columnId)
            .OrderBy(x => x.SortKey)
            .Include(x => x.CardType)
            .Include(x => x.CardTags)
                .ThenInclude(x => x.Tag)
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
            .Include(x => x.CardType)
            .Include(x => x.CardTags)
                .ThenInclude(x => x.Tag)
            .ToListAsync();
    }

}
