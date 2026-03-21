using BoardOil.Abstractions.Entities;
using BoardOil.Abstractions.DataAccess;

namespace BoardOil.Abstractions.Users;

public interface IUserRepository : IRepositoryBase<BoardUser>
{
    Task<IReadOnlyList<BoardUser>> GetUsersOrderedAsync();
    Task<bool> UserNameExistsAsync(string userName);
    Task<BoardUser?> GetByIdAsync(int id);
    Task<int> CountActiveAdminsAsync();
}
