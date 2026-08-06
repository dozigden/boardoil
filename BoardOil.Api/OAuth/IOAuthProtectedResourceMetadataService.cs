namespace BoardOil.Api.OAuth;

public interface IOAuthProtectedResourceMetadataService
{
    Task<OAuthProtectedResourceMetadata?> GetAsync(string publicId, HttpRequest request);
}
