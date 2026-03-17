using BoardOil.Ef;
using BoardOil.Ef.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Services.Card;

public sealed class CardRepository(BoardOilDbContext dbContext) : ICardRepository
{
    public Task<BoardCard?> GetByIdAsync(int id) =>
        dbContext.Cards.FirstOrDefaultAsync(x => x.Id == id);

    public Task<bool> ColumnExistsAsync(int columnId) =>
        dbContext.Columns.AnyAsync(x => x.Id == columnId);

    public async Task<IReadOnlyList<BoardCard>> GetCardsInColumnOrderedAsync(int columnId) =>
        await dbContext.Cards
            .Where(x => x.BoardColumnId == columnId)
            .OrderBy(x => x.SortKey)
            .ToListAsync();

    public async Task<IReadOnlyList<BoardCard>> GetCardsForColumnsOrderedAsync(IReadOnlyList<int> columnIds)
    {
        if (columnIds.Count == 0)
        {
            return Array.Empty<BoardCard>();
        }

        return await dbContext.Cards
            .Where(x => columnIds.Contains(x.BoardColumnId))
            .OrderBy(x => x.SortKey)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<int>> GetCardIdsInColumnOrderedAsync(int columnId) =>
        await dbContext.Cards
            .Where(x => x.BoardColumnId == columnId)
            .OrderBy(x => x.SortKey)
            .Select(x => x.Id)
            .ToListAsync();

    public void Add(BoardCard card) => dbContext.Cards.Add(card);

    public void Remove(BoardCard card) => dbContext.Cards.Remove(card);

    public Task SaveChangesAsync() =>
        dbContext.SaveChangesAsync();
}
