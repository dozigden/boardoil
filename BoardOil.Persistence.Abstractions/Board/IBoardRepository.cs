using BoardOil.Persistence.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Persistence.Abstractions.Board;

public interface IBoardRepository : IRepositoryBase<EntityBoard>
{
    Task<IReadOnlyList<EntityBoard>> GetBoardsOrderedAsync();
    Task<bool> AnyBoardAsync();
}
