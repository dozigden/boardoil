using BoardOil.Services.Contracts;

namespace BoardOil.Services.Abstractions;

public interface ICardService
{
    Task<ApiResult<CardDto>> CreateCardAsync(CreateCardRequest request);
    Task<ApiResult<CardDto>> UpdateCardAsync(int id, UpdateCardRequest request);
    Task<ApiResult> DeleteCardAsync(int id);
}
