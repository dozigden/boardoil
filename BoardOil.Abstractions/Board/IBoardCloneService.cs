using BoardOil.Contracts.Board;
using BoardOil.Contracts.Common;

namespace BoardOil.Abstractions.Board;

public interface IBoardCloneService
{
    Task<ApiResult<BoardDto>> CloneBoardAsync(int sourceBoardId, CloneBoardRequest request, int actorUserId);
}
