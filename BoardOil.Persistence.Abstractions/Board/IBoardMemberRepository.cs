using BoardOil.Persistence.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Persistence.Abstractions.Board;

public interface IBoardMemberRepository : IRepositoryBase<EntityBoardMember>
{
    Task<IReadOnlyList<EntityBoardMember>> GetMembershipsForUserOrderedAsync(int userId);
    Task<IReadOnlyList<EntityBoardMember>> GetMembersInBoardAsync(int boardId);
    Task<EntityBoardMember?> GetByBoardAndUserAsync(int boardId, int userId);
    Task<int> CountOwnersAsync(int boardId);
}
