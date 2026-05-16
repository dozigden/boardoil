using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.Auth;

public interface IAuthUserRepository : IRepositoryBase<EntityUser>
{
    Task<bool> AnyAsync();
    Task<EntityUser?> GetByUserNameAsync(string userName);
    Task<bool> NormalisedEmailExistsAsync(string normalisedEmail);
}
