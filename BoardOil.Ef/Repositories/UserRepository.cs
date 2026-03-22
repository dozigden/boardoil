using BoardOil.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Persistence.Abstractions.Users;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class UserRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityUser>(ambientDbContextLocator), IUserRepository
{
    public async Task<IReadOnlyList<EntityUser>> GetUsersOrderedAsync() =>
        await DbSet
            .OrderBy(x => x.UserName)
            .ToListAsync();

    public Task<bool> UserNameExistsAsync(string userName) =>
        DbSet.AnyAsync(x => x.UserName == userName);

    public Task<int> CountActiveAdminsAsync() =>
        DbSet.CountAsync(x => x.IsActive && x.Role == UserRole.Admin);
}
