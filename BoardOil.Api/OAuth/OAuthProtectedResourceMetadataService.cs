using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Mcp;

namespace BoardOil.Api.OAuth;

public sealed class OAuthProtectedResourceMetadataService(
    IMcpProjectConnectionRepository connectionRepository,
    IDbContextScopeFactory scopeFactory,
    OAuthEndpointUrlResolver urlResolver) : IOAuthProtectedResourceMetadataService
{
    public async Task<OAuthProtectedResourceMetadata?> GetAsync(string publicId, HttpRequest request)
    {
        if (string.IsNullOrWhiteSpace(publicId) || publicId.Length != 64)
        {
            return null;
        }

        using var scope = scopeFactory.CreateReadOnly();
        var connection = await connectionRepository.GetByPublicIdAsync(publicId);
        if (connection is null
            || connection.RevokedAtUtc is not null
            || !connection.ClientAccount.IsActive)
        {
            return null;
        }

        var publicBaseUrl = await urlResolver.GetPublicBaseUrlAsync(request);
        var resource = $"{publicBaseUrl}/mcp/connections/{connection.PublicId}";
        var issuer = $"{publicBaseUrl}/";
        var scopes = connection.AllowedScopesCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new OAuthProtectedResourceMetadata(resource, [issuer], scopes, ["header"]);
    }
}
