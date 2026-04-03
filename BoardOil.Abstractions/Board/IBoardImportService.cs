using BoardOil.Contracts.Board;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Abstractions.Board;

public interface IBoardImportService
{
    Task<ApiResult<BoardDto>> ImportTasksMdBoardAsync(ImportTasksMdBoardRequest request, int actorUserId);
}
