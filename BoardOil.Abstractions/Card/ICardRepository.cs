using BoardOil.Contracts.Card;

namespace BoardOil.Abstractions.Card;

public interface ICardRepository
{
    Task<CardRecord?> GetByIdAsync(int id);
    Task<bool> ColumnExistsAsync(int columnId);
    Task<IReadOnlyList<CardRecord>> GetCardsInColumnOrderedAsync(int columnId);
    Task<IReadOnlyList<CardRecord>> GetCardsForColumnsOrderedAsync(IReadOnlyList<int> columnIds);
    Task<IReadOnlyList<int>> GetCardIdsInColumnOrderedAsync(int columnId);
    Task<CardRecord> CreateAsync(CreateCardRecord card);
    Task UpdateAsync(UpdateCardRecord card);
    Task DeleteAsync(int id);
}
