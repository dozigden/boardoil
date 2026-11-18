using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.Board;

public interface IBoardRepository : IRepositoryBase<EntityBoard>
{
    Task<IReadOnlyList<EntityBoard>> GetBoardsOrderedAsync();
    Task<IReadOnlyList<EntityBoard>> GetDeletedBoardsOrderedAsync();
    Task<EntityBoard?> GetIncludingDeletedAsync(int boardId);
    Task<bool> AnyBoardIncludingDeletedAsync();
    Task<IReadOnlyList<int>> GetBoardIdsIncludingDeletedAsync(CancellationToken cancellationToken = default);
    Task<EntityBoard?> GetForResaveAsync(int boardId, CancellationToken cancellationToken = default);
}
