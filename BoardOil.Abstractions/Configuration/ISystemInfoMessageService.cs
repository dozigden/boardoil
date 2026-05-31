using BoardOil.Contracts.Configuration;
using BoardOil.Contracts.Common;

namespace BoardOil.Abstractions.Configuration;

public interface ISystemInfoMessageService
{
    Task<ApiResult<SystemInfoMessageDto?>> GetAsync();
    Task<ApiResult<SystemInfoMessageDto?>> UpdateAsync(SystemInfoMessageDto? request);
}
