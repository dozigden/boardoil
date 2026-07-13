using BoardOil.Contracts.Common;
using BoardOil.Contracts.Slick;
using BoardOil.Contracts.Style;

namespace BoardOil.Abstractions.Slick;

public interface ISlickService
{
    Task<ApiResult<IReadOnlyList<SlickDto>>> GetSlicksAsync(int boardId, int actorUserId);
    Task<ApiResult<StyleDefaultDto>> GetCreateDefaultStyleAsync(int boardId, int actorUserId);
    Task<ApiResult<SlickDto>> CreateSlickAsync(int boardId, CreateSlickRequest request, int actorUserId);
    Task<ApiResult<SlickDto>> UpdateSlickAsync(int boardId, int slickId, UpdateSlickRequest request, int actorUserId);
    Task<ApiResult> DeleteSlickAsync(int boardId, int slickId, int actorUserId);
}
