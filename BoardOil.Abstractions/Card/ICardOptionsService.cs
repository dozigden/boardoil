using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;

namespace BoardOil.Abstractions.Card;

public interface ICardOptionsService
{
    Task<ApiResult<BoardCardOptionsDto>> GetOptionsAsync(int boardId, int actorUserId);
}
