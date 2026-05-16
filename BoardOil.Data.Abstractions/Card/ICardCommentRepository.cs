using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.Card;

public interface ICardCommentRepository : IRepositoryBase<EntityCardComment>
{
    Task<IReadOnlyList<EntityCardComment>> GetForCardOrderedAsync(int cardId);
    Task<IReadOnlyList<EntityCardComment>> GetForCardsOrderedAsync(IReadOnlyList<int> cardIds);
    Task<EntityCardComment?> GetByIdWithAuthorAsync(int id);
}
