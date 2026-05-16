using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.Slick;

public interface ISlickRepository : IRepositoryBase<EntitySlick>
{
    Task<IReadOnlyList<EntitySlick>> GetAllForBoardAsync(int boardId);
    Task<EntitySlick?> GetByIdInBoardAsync(int boardId, int slickId);
    Task<EntitySlick?> GetByNormalisedNameAsync(int boardId, string normalisedName);
}
