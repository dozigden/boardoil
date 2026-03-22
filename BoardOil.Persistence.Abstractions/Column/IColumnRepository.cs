using BoardOil.Persistence.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Persistence.Abstractions.Column;

public interface IColumnRepository : IRepositoryBase<EntityBoardColumn>
{
    Task<EntityBoardColumn?> GetByIdAsync(int id);
    Task<IReadOnlyList<EntityBoardColumn>> GetColumnsInBoardOrderedAsync(int boardId);
}
