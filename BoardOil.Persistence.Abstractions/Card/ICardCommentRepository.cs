using BoardOil.Persistence.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Persistence.Abstractions.Card;

public interface ICardCommentRepository : IRepositoryBase<EntityCardComment>
{
    Task<IReadOnlyList<EntityCardComment>> GetForCardOrderedAsync(int cardId);
    Task<IReadOnlyList<EntityCardComment>> GetForCardsOrderedAsync(IReadOnlyList<int> cardIds);
    Task<EntityCardComment?> GetByIdWithAuthorAsync(int id);
}
