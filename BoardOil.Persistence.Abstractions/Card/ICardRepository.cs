using BoardOil.Persistence.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Persistence.Abstractions.Card;

public interface ICardRepository : IRepositoryBase<EntityBoardCard>
{
    Task<EntityBoardCard?> GetWithTagsByIdAsync(int id);
    Task<EntityBoardCard?> GetWithTagsAndBoardAsync(int id);
    Task<IReadOnlyList<EntityBoardCard>> GetByBoardAndCardTypeAsync(int boardId, int cardTypeId);
    Task<bool> ColumnExistsAsync(int columnId);
    Task<IReadOnlyList<EntityBoardCard>> GetCardsInColumnOrderedAsync(int columnId);
    Task<IReadOnlyList<EntityBoardCard>> GetCardsForColumnsOrderedAsync(IReadOnlyList<int> columnIds);
}
