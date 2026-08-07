using BoardOil.Contracts.Auth;

namespace BoardOil.Api.OAuth;

public sealed class OAuthProtectedResourceMetadataService(
    OAuthEndpointUrlResolver urlResolver) : IOAuthProtectedResourceMetadataService
{
    public async Task<OAuthProtectedResourceMetadata> GetMcpAsync(HttpRequest request)
    {
        var publicBaseUrl = await urlResolver.GetPublicBaseUrlAsync(request);
        var resource = $"{publicBaseUrl}{OAuthResources.McpPath}";
        var issuer = $"{publicBaseUrl}/";
        return new OAuthProtectedResourceMetadata(
            resource,
            [issuer],
            [MachinePatScopes.McpRead, MachinePatScopes.McpWrite],
            ["header"]);
    }
}
