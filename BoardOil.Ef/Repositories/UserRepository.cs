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

    public Task<EntityUser?> GetByNormalisedEmailAsync(string normalisedEmail) =>
        DbSet.SingleOrDefaultAsync(x => x.NormalisedEmail == normalisedEmail);

    public Task<bool> UserNameExistsAsync(string userName) =>
        DbSet.AnyAsync(x => x.UserName == userName);

    public Task<bool> NormalisedEmailExistsAsync(string normalisedEmail) =>
        DbSet.AnyAsync(x => x.NormalisedEmail == normalisedEmail);

    public Task<bool> NormalisedEmailExistsForOtherUserAsync(int userId, string normalisedEmail) =>
        DbSet.AnyAsync(x => x.Id != userId && x.NormalisedEmail == normalisedEmail);

    public Task<int> CountActiveAdminsAsync() =>
        DbSet.CountAsync(x => x.IsActive && x.Role == UserRole.Admin && x.IdentityType == UserIdentityType.User);
}
