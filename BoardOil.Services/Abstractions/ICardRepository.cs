using BoardOil.Ef.Entities;

namespace BoardOil.Services.Abstractions;

public interface ICardRepository
{
    Task<BoardCard?> GetByIdAsync(int id);
    Task<bool> ColumnExistsAsync(int columnId);
    Task<IReadOnlyList<BoardCard>> GetCardsInColumnOrderedAsync(int columnId);
    Task<IReadOnlyList<int>> GetCardIdsInColumnOrderedAsync(int columnId);
    void Add(BoardCard card);
    void Remove(BoardCard card);
    Task SaveChangesAsync();
}
