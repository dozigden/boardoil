using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.OAuth;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class OAuthConnectionRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityOAuthConnection>(ambientDbContextLocator), IOAuthConnectionRepository
{
    public Task<EntityOAuthConnection?> GetByUserResourceAndNameAsync(
        int userId,
        string resourceType,
        string normalisedName) =>
        DbSet
            .Include(x => x.User)
            .Include(x => x.ActiveGrant)
            .SingleOrDefaultAsync(x => x.UserId == userId
                && x.ResourceType == resourceType
                && x.NormalisedName == normalisedName);

    public Task<EntityOAuthConnectionGrant?> GetGrantByAuthorizationIdAsync(string authorizationId) =>
        DbContext.Set<EntityOAuthConnectionGrant>()
            .Include(x => x.OAuthConnection)
                .ThenInclude(x => x.User)
            .SingleOrDefaultAsync(x => x.OpenIddictAuthorizationId == authorizationId);

    public Task<bool> AnyGrantForApplicationAsync(string applicationId) =>
        DbContext.Set<EntityOAuthConnectionGrant>()
            .AnyAsync(x => x.OpenIddictApplicationId == applicationId);

}
