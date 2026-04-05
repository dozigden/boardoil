using BoardOil.Contracts.CardType;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Abstractions.CardType;

public interface ICardTypeService
{
    Task<ApiResult<IReadOnlyList<CardTypeDto>>> GetCardTypesAsync(int boardId, int actorUserId);
    Task<ApiResult<CardTypeDto>> CreateCardTypeAsync(int boardId, CreateCardTypeRequest request, int actorUserId);
    Task<ApiResult<CardTypeDto>> UpdateCardTypeAsync(int boardId, int cardTypeId, UpdateCardTypeRequest request, int actorUserId);
    Task<ApiResult> DeleteCardTypeAsync(int boardId, int cardTypeId, int actorUserId);
}
