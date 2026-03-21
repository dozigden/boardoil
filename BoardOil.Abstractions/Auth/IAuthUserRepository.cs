using BoardOil.Abstractions.Entities;
using BoardOil.Abstractions.DataAccess;

namespace BoardOil.Abstractions.Auth;

public interface IAuthUserRepository : IRepositoryBase<BoardUser>
{
    Task<bool> AnyAsync();
    Task<BoardUser?> GetByUserNameAsync(string userName);
    Task<BoardUser?> GetActiveByIdAsync(int id);
}
