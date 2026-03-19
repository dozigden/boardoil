using BoardOil.Contracts.Configuration;

namespace BoardOil.Api.Configuration;

public sealed class ConfigurationService(JwtAuthOptions jwtOptions) : IConfigurationService
{
    public ConfigurationDto GetConfiguration() =>
        new(jwtOptions.AllowInsecureCookies);
}
