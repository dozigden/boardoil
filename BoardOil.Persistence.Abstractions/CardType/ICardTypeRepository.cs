using BoardOil.Persistence.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Persistence.Abstractions.CardType;

public interface ICardTypeRepository : IRepositoryBase<EntityCardType>
{
    Task<EntityCardType?> GetSystemByBoardIdAsync(int boardId);
}
