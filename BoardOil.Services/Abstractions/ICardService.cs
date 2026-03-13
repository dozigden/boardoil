using BoardOil.Services.Contracts;

namespace BoardOil.Services.Abstractions;

public interface ICardService
{
    Task<ApiResult<CardDto>> CreateCardAsync(CreateCardRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<CardDto>> UpdateCardAsync(int id, UpdateCardRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteCardAsync(int id, CancellationToken cancellationToken = default);
}
