using BoardOil.Ef.Entities;

namespace BoardOil.Services.Abstractions;

public interface IAuthRepository
{
    Task<bool> AnyUsersAsync();
    void AddUser(BoardUser user);
    Task<BoardUser?> GetUserByUserNameAsync(string userName);
    Task<BoardUser?> GetActiveUserByIdAsync(int id);

    void AddRefreshToken(RefreshToken refreshToken);
    Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash);
    Task<RefreshToken?> GetRefreshTokenWithUserByHashAsync(string tokenHash);

    Task SaveChangesAsync();
}
