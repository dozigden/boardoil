using BoardOil.Contracts.Column;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Abstractions.Column;

public interface IColumnService
{
    Task<ApiResult<IReadOnlyList<ColumnDto>>> GetColumnsAsync();
    Task<ApiResult<ColumnDto>> CreateColumnAsync(CreateColumnRequest request);
    Task<ApiResult<ColumnDto>> UpdateColumnAsync(int id, UpdateColumnRequest request);
    Task<ApiResult> DeleteColumnAsync(int id);
}
