using BoardOil.Persistence.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Persistence.Abstractions.Users;

public interface IUserRepository : IRepositoryBase<EntityUser>
{
    Task<IReadOnlyList<EntityUser>> GetUsersOrderedAsync();
    Task<bool> UserNameExistsAsync(string userName);
    Task<bool> NormalisedEmailExistsAsync(string normalisedEmail);
    Task<bool> NormalisedEmailExistsForOtherUserAsync(int userId, string normalisedEmail);
    Task<int> CountActiveAdminsAsync();
}
