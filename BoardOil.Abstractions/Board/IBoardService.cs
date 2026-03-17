using BoardOil.Contracts.Board;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Abstractions.Board;

public interface IBoardService
{
    Task<ApiResult<BoardDto>> GetBoardAsync();
}
