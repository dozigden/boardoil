using BoardOil.Ef.Entities;

namespace BoardOil.Services.Card;

public interface ICardRepository
{
    Task<BoardCard?> GetByIdAsync(int id);
    Task<bool> ColumnExistsAsync(int columnId);
    Task<IReadOnlyList<BoardCard>> GetCardsInColumnOrderedAsync(int columnId);
    Task<IReadOnlyList<BoardCard>> GetCardsForColumnsOrderedAsync(IReadOnlyList<int> columnIds);
    Task<IReadOnlyList<int>> GetCardIdsInColumnOrderedAsync(int columnId);
    void Add(BoardCard card);
    void Remove(BoardCard card);
    Task SaveChangesAsync();
}
