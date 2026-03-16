using BoardOil.Ef.Entities;

namespace BoardOil.Services.Abstractions;

public interface IUserRepository
{
    Task<IReadOnlyList<BoardUser>> GetUsersOrderedAsync();
    Task<bool> UserNameExistsAsync(string userName);
    void Add(BoardUser user);
    Task<BoardUser?> GetByIdAsync(int id);
    Task<int> CountActiveAdminsAsync();
    Task SaveChangesAsync();
}
