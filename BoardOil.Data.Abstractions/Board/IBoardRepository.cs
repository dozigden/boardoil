using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.Board;

public interface IBoardRepository : IRepositoryBase<EntityBoard>
{
    Task<IReadOnlyList<EntityBoard>> GetBoardsOrderedAsync();
    Task<IReadOnlyList<EntityBoard>> GetBoardsForUserOrderedAsync(int userId);
    Task<IReadOnlyList<EntityBoard>> GetBoardsByIdsOrderedAsync(IReadOnlyList<int> boardIds);
    Task<bool> AnyBoardAsync();
}
