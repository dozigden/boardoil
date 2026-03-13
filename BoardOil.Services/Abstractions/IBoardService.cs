using BoardOil.Services.Contracts;

namespace BoardOil.Services.Abstractions;

public interface IBoardService
{
    Task<ApiResult<BoardDto>> GetBoardAsync(CancellationToken cancellationToken = default);
}
