using BoardOil.Abstractions.Auth;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class AuthUserRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<BoardUser>(ambientDbContextLocator), IAuthUserRepository
{
    public Task<bool> AnyAsync() =>
        DbSet.AnyAsync();

    public Task<BoardUser?> GetByUserNameAsync(string userName) =>
        DbSet.FirstOrDefaultAsync(x => x.UserName == userName);

    public Task<BoardUser?> GetActiveByIdAsync(int id) =>
        DbSet.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
}
