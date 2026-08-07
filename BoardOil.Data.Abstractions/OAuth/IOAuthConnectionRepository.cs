using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.OAuth;

public interface IOAuthConnectionRepository : IRepositoryBase<EntityOAuthConnection>
{
    Task<EntityOAuthConnection?> GetByUserResourceAndNameAsync(
        int userId,
        string resourceType,
        string normalisedName);

    Task<EntityOAuthConnectionGrant?> GetGrantByAuthorizationIdAsync(string authorizationId);
    Task<bool> AnyGrantForApplicationAsync(string applicationId);
}
