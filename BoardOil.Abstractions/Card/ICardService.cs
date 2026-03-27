using BoardOil.Contracts.Card;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Abstractions.Card;

public interface ICardService
{
    Task<ApiResult<CardDto>> CreateCardAsync(int boardId, CreateCardRequest request);
    Task<ApiResult<CardDto>> UpdateCardAsync(int boardId, int id, UpdateCardRequest request);
    Task<ApiResult<CardDto>> MoveCardAsync(int boardId, int id, MoveCardRequest request);
    Task<ApiResult> DeleteCardAsync(int boardId, int id);
}
