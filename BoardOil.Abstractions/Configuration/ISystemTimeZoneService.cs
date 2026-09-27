using BoardOil.Contracts.Common;
using BoardOil.Contracts.Configuration;

namespace BoardOil.Abstractions.Configuration;

public interface ISystemTimeZoneService
{
    ApiError? Validate(string? timeZoneId);
    ApiResult<SystemTimeZoneOptionsDto> GetOptions();
    Task<TimeZoneInfo> GetConfiguredTimeZoneAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<SystemTimeZoneDto>> GetAsync(CancellationToken cancellationToken = default);
    // Stages a validated change in the caller's write scope. The caller must hold
    // SchedulingGate until that scope is saved and disposed.
    Task<bool> ApplyValidatedChangeAsync(string timeZoneId, CancellationToken cancellationToken = default);
    Task<ApiResult<SystemTimeZoneDto>> UpdateAsync(
        UpdateSystemTimeZoneRequest request, CancellationToken cancellationToken = default);
}
