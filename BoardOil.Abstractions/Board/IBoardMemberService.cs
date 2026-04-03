using BoardOil.Contracts.Board;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Abstractions.Board;

public interface IBoardMemberService
{
    Task<ApiResult<IReadOnlyList<BoardMemberDto>>> GetMembersAsync(int boardId, int actorUserId);
    Task<ApiResult<BoardMemberDto>> AddMemberAsync(int boardId, AddBoardMemberRequest request, int actorUserId);
    Task<ApiResult<BoardMemberDto>> UpdateMemberRoleAsync(int boardId, int userId, UpdateBoardMemberRoleRequest request, int actorUserId);
    Task<ApiResult> RemoveMemberAsync(int boardId, int userId, int actorUserId);
}
