using BoardOil.Ef;
using BoardOil.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Services.Users;

public sealed class UserRepository(BoardOilDbContext dbContext) : IUserRepository
{
    public async Task<IReadOnlyList<BoardUser>> GetUsersOrderedAsync() =>
        await dbContext.Users
            .OrderBy(x => x.UserName)
            .ToListAsync();

    public Task<bool> UserNameExistsAsync(string userName) =>
        dbContext.Users.AnyAsync(x => x.UserName == userName);

    public void Add(BoardUser user) =>
        dbContext.Users.Add(user);

    public Task<BoardUser?> GetByIdAsync(int id) =>
        dbContext.Users.FirstOrDefaultAsync(x => x.Id == id);

    public Task<int> CountActiveAdminsAsync() =>
        dbContext.Users.CountAsync(x => x.IsActive && x.Role == UserRole.Admin);

    public Task SaveChangesAsync() =>
        dbContext.SaveChangesAsync();
}
