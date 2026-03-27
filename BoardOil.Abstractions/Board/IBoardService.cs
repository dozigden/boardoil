using BoardOil.Contracts.Board;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Abstractions.Board;

public interface IBoardService
{
    Task<ApiResult<IReadOnlyList<BoardSummaryDto>>> GetBoardsAsync();
    Task<ApiResult<BoardDto>> GetBoardAsync(int boardId);
    Task<ApiResult<BoardDto>> CreateBoardAsync(CreateBoardRequest request);
}
