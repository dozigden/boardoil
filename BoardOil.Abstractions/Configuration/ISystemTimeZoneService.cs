using BoardOil.Contracts.Common;
using BoardOil.Contracts.Configuration;

namespace BoardOil.Abstractions.Configuration;

public interface ISystemTimeZoneService
{
    ApiError? Validate(string? timeZoneId);
    ApiResult<SystemTimeZoneOptionsDto> GetOptions();
    Task<TimeZoneInfo> GetConfiguredTimeZoneAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<SystemTimeZoneDto>> GetAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<SystemTimeZoneDto>> UpdateAsync(
        UpdateSystemTimeZoneRequest request, CancellationToken cancellationToken = default);
}
