using BoardOil.Abstractions.Entities;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Users;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class UserRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<BoardUser>(ambientDbContextLocator), IUserRepository
{
    public async Task<IReadOnlyList<BoardUser>> GetUsersOrderedAsync() =>
        await DbSet
            .OrderBy(x => x.UserName)
            .ToListAsync();

    public Task<bool> UserNameExistsAsync(string userName) =>
        DbSet.AnyAsync(x => x.UserName == userName);

    public Task<BoardUser?> GetByIdAsync(int id) =>
        DbSet.FirstOrDefaultAsync(x => x.Id == id);

    public Task<int> CountActiveAdminsAsync() =>
        DbSet.CountAsync(x => x.IsActive && x.Role == UserRole.Admin);
}
