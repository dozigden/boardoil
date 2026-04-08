using BoardOil.Contracts.Board;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Abstractions.Board;

public interface IBoardExportService
{
    Task<ApiResult<BoardPackageExportDto>> ExportBoardAsync(int boardId, int actorUserId, string exportedByVersion);
}
