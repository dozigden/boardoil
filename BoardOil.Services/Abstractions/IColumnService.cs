using BoardOil.Services.Contracts;

namespace BoardOil.Services.Abstractions;

public interface IColumnService
{
    Task<ApiResult<IReadOnlyList<ColumnDto>>> GetColumnsAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<ColumnDto>> CreateColumnAsync(CreateColumnRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<ColumnDto>> UpdateColumnAsync(int id, UpdateColumnRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteColumnAsync(int id, CancellationToken cancellationToken = default);
}
