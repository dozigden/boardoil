using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.Column;

public interface IColumnRepository : IRepositoryBase<EntityBoardColumn>
{
    Task<IReadOnlyList<EntityBoardColumn>> GetColumnsInBoardOrderedAsync(int boardId);
}
