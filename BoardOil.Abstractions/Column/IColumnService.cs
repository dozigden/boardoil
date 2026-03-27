using BoardOil.Contracts.Column;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Abstractions.Column;

public interface IColumnService
{
    Task<ApiResult<IReadOnlyList<ColumnDto>>> GetColumnsAsync(int boardId);
    Task<ApiResult<ColumnDto>> CreateColumnAsync(int boardId, CreateColumnRequest request);
    Task<ApiResult<ColumnDto>> UpdateColumnAsync(int boardId, int id, UpdateColumnRequest request);
    Task<ApiResult<ColumnDto>> MoveColumnAsync(int boardId, int id, MoveColumnRequest request);
    Task<ApiResult> DeleteColumnAsync(int boardId, int id);
}
