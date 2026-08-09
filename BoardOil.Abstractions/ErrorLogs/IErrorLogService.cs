using BoardOil.Contracts.Common;
using BoardOil.Contracts.ErrorLogs;

namespace BoardOil.Abstractions.ErrorLogs;

public interface IErrorLogService
{
    Task<ApiResult<ErrorLogListDto>> ListAsync(int? offset, int? limit);
    Task<ApiResult<ErrorLogDetailsDto>> GetAsync(int id);
    Task<ApiResult<ErrorLogPurgeResultDto>> PurgeExpiredAsync(CancellationToken cancellationToken = default);
    Task<int?> LogExceptionAsync(
        Exception exception,
        ErrorLogContext context,
        CancellationToken cancellationToken = default);
}
