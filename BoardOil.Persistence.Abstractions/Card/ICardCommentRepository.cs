using BoardOil.Persistence.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Persistence.Abstractions.Card;

public interface ICardCommentRepository : IRepositoryBase<EntityCardComment>
{
    Task<IReadOnlyList<EntityCardComment>> GetForCardOrderedAsync(int cardId);
}
