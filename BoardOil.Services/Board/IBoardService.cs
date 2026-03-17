using BoardOil.Services.Contracts;

namespace BoardOil.Services.Board;

public interface IBoardService
{
    Task<ApiResult<BoardDto>> GetBoardAsync();
}
