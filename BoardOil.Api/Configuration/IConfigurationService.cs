using BoardOil.Contracts.Contracts;
using BoardOil.Contracts.Configuration;

namespace BoardOil.Api.Configuration;

public interface IConfigurationService
{
    Task<ApiResult<ConfigurationDto>> GetConfigurationAsync();
    Task<ApiResult<ConfigurationDto>> UpdateConfigurationAsync(UpdateConfigurationRequest request);
    Task<string?> GetMcpPublicBaseUrlAsync();
}
