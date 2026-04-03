using BoardOil.Contracts.Board;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Abstractions.Board;

public interface IBoardService
{
    Task<ApiResult<IReadOnlyList<BoardSummaryDto>>> GetBoardsAsync(int actorUserId);
    Task<ApiResult<BoardDto>> GetBoardAsync(int boardId, int actorUserId);
    Task<ApiResult<BoardDto>> CreateBoardAsync(CreateBoardRequest request, int actorUserId);
    Task<ApiResult<BoardSummaryDto>> UpdateBoardAsync(int boardId, UpdateBoardRequest request, int actorUserId);
    Task<ApiResult> DeleteBoardAsync(int boardId, int actorUserId);
}
