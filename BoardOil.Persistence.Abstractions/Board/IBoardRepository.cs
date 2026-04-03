using BoardOil.Persistence.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Persistence.Abstractions.Board;

public interface IBoardRepository : IRepositoryBase<EntityBoard>
{
    Task<IReadOnlyList<EntityBoard>> GetBoardsOrderedAsync();
    Task<IReadOnlyList<EntityBoard>> GetBoardsForUserOrderedAsync(int userId);
    Task<IReadOnlyList<EntityBoard>> GetBoardsByIdsOrderedAsync(IReadOnlyList<int> boardIds);
    Task<bool> AnyBoardAsync();
}
