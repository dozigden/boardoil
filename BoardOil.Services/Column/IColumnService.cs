using BoardOil.Services.Contracts;

namespace BoardOil.Services.Column;

public interface IColumnService
{
    Task<ApiResult<IReadOnlyList<ColumnDto>>> GetColumnsAsync();
    Task<ApiResult<ColumnDto>> CreateColumnAsync(CreateColumnRequest request);
    Task<ApiResult<ColumnDto>> UpdateColumnAsync(int id, UpdateColumnRequest request);
    Task<ApiResult> DeleteColumnAsync(int id);
}
