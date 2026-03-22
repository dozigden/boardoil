using BoardOil.Persistence.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Persistence.Abstractions.Auth;

public interface IAuthUserRepository : IRepositoryBase<EntityUser>
{
    Task<bool> AnyAsync();
    Task<EntityUser?> GetByUserNameAsync(string userName);
}
