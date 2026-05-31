using BoardOil.Contracts.Board;
using BoardOil.Contracts.Common;

namespace BoardOil.Abstractions.Board;

public interface IBoardPackageImportService
{
    Task<ApiResult<BoardDto>> ImportBoardPackageAsync(ImportBoardPackageRequest request, int actorUserId);
}
