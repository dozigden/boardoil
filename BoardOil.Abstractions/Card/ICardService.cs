using BoardOil.Contracts.Card;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Abstractions.Card;

public interface ICardService
{
    Task<ApiResult<CardDto>> CreateCardAsync(CreateCardRequest request);
    Task<ApiResult<CardDto>> UpdateCardAsync(int id, UpdateCardRequest request);
    Task<ApiResult<CardDto>> MoveCardAsync(int id, MoveCardRequest request);
    Task<ApiResult> DeleteCardAsync(int id);
}
