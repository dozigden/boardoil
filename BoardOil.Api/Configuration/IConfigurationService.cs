using BoardOil.Contracts.Configuration;

namespace BoardOil.Api.Configuration;

public interface IConfigurationService
{
    ConfigurationDto GetConfiguration();
}
