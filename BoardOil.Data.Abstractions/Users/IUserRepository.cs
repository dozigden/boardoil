using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.Users;

public interface IUserRepository : IRepositoryBase<EntityUser>
{
    Task<IReadOnlyList<EntityUser>> GetUsersOrderedAsync();
    Task<EntityUser?> GetByNormalisedEmailAsync(string normalisedEmail);
    Task<bool> UserNameExistsAsync(string userName);
    Task<bool> NormalisedEmailExistsAsync(string normalisedEmail);
    Task<bool> NormalisedEmailExistsForOtherUserAsync(int userId, string normalisedEmail);
    Task<int> CountActiveAdminsAsync();
}
