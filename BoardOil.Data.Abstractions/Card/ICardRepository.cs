using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.Card;

public interface ICardRepository : IRepositoryBase<EntityBoardCard>
{
    Task<EntityBoardCard?> GetWithTagsByIdAsync(int id);
    Task<EntityBoardCard?> GetWithTagsAndBoardAsync(int id);
    Task<IReadOnlyList<EntityBoardCard>> GetWithTagsAndBoardByIdsAsync(IReadOnlyList<int> ids);
    Task<IReadOnlyList<EntityBoardCard>> GetByBoardAndCardTypeAsync(int boardId, int cardTypeId);
    Task<bool> ColumnExistsAsync(int columnId);
    Task<IReadOnlyList<EntityBoardCard>> GetCardsInColumnOrderedAsync(int columnId);
    Task<IReadOnlyList<EntityBoardCard>> GetCardsForColumnsOrderedAsync(IReadOnlyList<int> columnIds);
}
