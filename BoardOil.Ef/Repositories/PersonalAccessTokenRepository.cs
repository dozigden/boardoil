using BoardOil.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Auth;
using BoardOil.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class PersonalAccessTokenRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityPersonalAccessToken>(ambientDbContextLocator), IPersonalAccessTokenRepository
{
    public async Task<IReadOnlyList<EntityPersonalAccessToken>> GetByUserIdAsync(int userId) =>
        await DbSet
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

    public Task<EntityPersonalAccessToken?> GetByIdAsync(int id) =>
        DbSet.FirstOrDefaultAsync(x => x.Id == id);

    public Task<EntityPersonalAccessToken?> GetWithUserByHashAsync(string tokenHash) =>
        DbSet
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);
}
