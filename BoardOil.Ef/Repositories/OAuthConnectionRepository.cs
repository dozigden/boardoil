using System.Linq.Expressions;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.OAuth;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class OAuthConnectionRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityOAuthConnection>(ambientDbContextLocator), IOAuthConnectionRepository
{
    public async Task<IReadOnlyList<EntityOAuthConnection>> GetAllActiveAsync() =>
        await WithActiveGrant()
            .Where(IsActiveConnection())
            .OrderBy(x => x.User.UserName)
            .ThenBy(x => x.Name)
            .ToListAsync();

    public async Task<IReadOnlyList<EntityOAuthConnection>> GetActiveForUserAsync(int userId) =>
        await WithActiveGrant()
            .Where(x => x.UserId == userId)
            .Where(IsActiveConnection())
            .OrderBy(x => x.Name)
            .ToListAsync();

    public Task<EntityOAuthConnection?> GetByIdWithActiveGrantAsync(int id) =>
        WithActiveGrant().SingleOrDefaultAsync(x => x.Id == id);

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

    private IQueryable<EntityOAuthConnection> WithActiveGrant() =>
        DbSet
            .Include(x => x.User)
            .Include(x => x.ActiveGrant);

    private static Expression<Func<EntityOAuthConnection, bool>> IsActiveConnection() =>
        connection => connection.RevokedAtUtc == null
            && connection.ActiveGrantId != null
            && connection.ActiveGrant != null
            && connection.ActiveGrant.RevokedAtUtc == null;
}
