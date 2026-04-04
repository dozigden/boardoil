using BoardOil.Contracts.Board;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Abstractions.Board;

public interface ISystemBoardService
{
    Task<ApiResult<IReadOnlyList<SystemBoardSummaryDto>>> GetBoardsAsync();
    Task<ApiResult<IReadOnlyList<BoardMemberDto>>> GetMembersAsync(int boardId);
    Task<ApiResult<BoardMemberDto>> AddMemberAsync(int boardId, AddBoardMemberRequest request);
    Task<ApiResult<BoardMemberDto>> UpdateMemberRoleAsync(int boardId, int userId, UpdateBoardMemberRoleRequest request);
    Task<ApiResult> RemoveMemberAsync(int boardId, int userId);
}
