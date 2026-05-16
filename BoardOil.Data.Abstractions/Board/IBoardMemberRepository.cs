using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.Board;

public interface IBoardMemberRepository : IRepositoryBase<EntityBoardMember>
{
    Task<IReadOnlyList<EntityBoardMember>> GetMembershipsForUserOrderedAsync(int userId);
    Task<IReadOnlyList<EntityBoardMember>> GetMembersInBoardAsync(int boardId);
    Task<EntityBoardMember?> GetByBoardAndUserAsync(int boardId, int userId);
    Task<int> CountOwnersAsync(int boardId);
}
