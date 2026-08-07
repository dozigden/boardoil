namespace BoardOil.Api.OAuth;

public interface IOAuthProtectedResourceMetadataService
{
    Task<OAuthProtectedResourceMetadata> GetMcpAsync(HttpRequest request);
}
