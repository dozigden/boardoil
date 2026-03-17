using BoardOil.Abstractions.Entities;

namespace BoardOil.Abstractions.Auth;

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
