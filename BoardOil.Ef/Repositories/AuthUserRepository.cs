using BoardOil.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Auth;
using BoardOil.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class AuthUserRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityUser>(ambientDbContextLocator), IAuthUserRepository
{
    public Task<bool> AnyAsync() =>
        DbSet.AnyAsync();

    public Task<EntityUser?> GetByUserNameAsync(string userName) =>
        DbSet.FirstOrDefaultAsync(x => x.UserName == userName);

    public Task<bool> NormalisedEmailExistsAsync(string normalisedEmail) =>
        DbSet.AnyAsync(x => x.NormalisedEmail == normalisedEmail);
}
