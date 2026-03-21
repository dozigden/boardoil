using BoardOil.Abstractions.Entities;

namespace BoardOil.Abstractions.Users;

public interface IUserRepository
{
    Task<IReadOnlyList<BoardUser>> GetUsersOrderedAsync();
    Task<bool> UserNameExistsAsync(string userName);
    void Add(BoardUser user);
    Task<BoardUser?> GetByIdAsync(int id);
    Task<int> CountActiveAdminsAsync();
}
