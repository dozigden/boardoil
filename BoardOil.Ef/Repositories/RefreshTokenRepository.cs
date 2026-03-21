using BoardOil.Abstractions.Auth;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class RefreshTokenRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<RefreshToken>(ambientDbContextLocator), IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByHashAsync(string tokenHash) =>
        DbSet.FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

    public Task<RefreshToken?> GetWithUserByHashAsync(string tokenHash) =>
        DbSet
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);
}
