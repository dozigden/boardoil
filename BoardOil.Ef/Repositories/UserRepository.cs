using BoardOil.Abstractions.Entities;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Users;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class UserRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase(ambientDbContextLocator), IUserRepository
{
    public async Task<IReadOnlyList<BoardUser>> GetUsersOrderedAsync() =>
        await DbContext.Users
            .OrderBy(x => x.UserName)
            .ToListAsync();

    public Task<bool> UserNameExistsAsync(string userName) =>
        DbContext.Users.AnyAsync(x => x.UserName == userName);

    public void Add(BoardUser user) =>
        DbContext.Users.Add(user);

    public Task<BoardUser?> GetByIdAsync(int id) =>
        DbContext.Users.FirstOrDefaultAsync(x => x.Id == id);

    public Task<int> CountActiveAdminsAsync() =>
        DbContext.Users.CountAsync(x => x.IsActive && x.Role == UserRole.Admin);
}
