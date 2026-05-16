using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.CardType;

public interface ICardTypeRepository : IRepositoryBase<EntityCardType>
{
    Task<IReadOnlyList<EntityCardType>> GetAllForBoardAsync(int boardId);
    Task<EntityCardType?> GetByIdInBoardAsync(int boardId, int cardTypeId);
    Task<EntityCardType?> GetByNormalisedNameAsync(int boardId, string normalisedName);
    Task<EntityCardType?> GetSystemByBoardIdAsync(int boardId);
}
