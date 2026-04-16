using BoardOil.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Auth;
using BoardOil.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class RefreshTokenRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityRefreshToken>(ambientDbContextLocator), IRefreshTokenRepository
{
    public Task<EntityRefreshToken?> GetByHashAsync(string tokenHash) =>
        DbSet.FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

    public Task<EntityRefreshToken?> GetWithUserByHashAsync(string tokenHash) =>
        DbSet
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

    public async Task RevokeActiveTokensByUserIdAsync(int userId, DateTime revokedAtUtc)
    {
        var activeTokens = await DbSet
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = revokedAtUtc;
        }
    }
}
