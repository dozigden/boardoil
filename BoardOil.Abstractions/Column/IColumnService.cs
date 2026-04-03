using BoardOil.Contracts.Column;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Abstractions.Column;

public interface IColumnService
{
    Task<ApiResult<IReadOnlyList<ColumnDto>>> GetColumnsAsync(int boardId, int actorUserId);
    Task<ApiResult<ColumnDto>> CreateColumnAsync(int boardId, CreateColumnRequest request, int actorUserId);
    Task<ApiResult<ColumnDto>> UpdateColumnAsync(int boardId, int id, UpdateColumnRequest request, int actorUserId);
    Task<ApiResult<ColumnDto>> MoveColumnAsync(int boardId, int id, MoveColumnRequest request, int actorUserId);
    Task<ApiResult> DeleteColumnAsync(int boardId, int id, int actorUserId);
}
