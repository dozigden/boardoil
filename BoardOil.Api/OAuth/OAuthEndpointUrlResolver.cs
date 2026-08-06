using BoardOil.Api.Configuration;

namespace BoardOil.Api.OAuth;

public sealed class OAuthEndpointUrlResolver(IConfigurationService configurationService)
{
    public async Task<string> GetPublicBaseUrlAsync(HttpRequest request)
    {
        var configuredBaseUrl = await configurationService.GetMcpPublicBaseUrlAsync();
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return configuredBaseUrl.TrimEnd('/');
        }

        return $"{request.Scheme}://{request.Host}{request.PathBase}".TrimEnd('/');
    }

    public async Task<string> ResolveAsync(HttpRequest request, string path) =>
        $"{await GetPublicBaseUrlAsync(request)}{path}";
}
