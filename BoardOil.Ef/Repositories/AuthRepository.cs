using BoardOil.Abstractions.Entities;
using BoardOil.Abstractions.Auth;
using BoardOil.Abstractions.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class AuthRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase(ambientDbContextLocator), IAuthRepository
{
    public Task<bool> AnyUsersAsync() =>
        DbContext.Users.AnyAsync();

    public void AddUser(BoardUser user) =>
        DbContext.Users.Add(user);

    public Task<BoardUser?> GetUserByUserNameAsync(string userName) =>
        DbContext.Users.FirstOrDefaultAsync(x => x.UserName == userName);

    public Task<BoardUser?> GetActiveUserByIdAsync(int id) =>
        DbContext.Users.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);

    public void AddRefreshToken(RefreshToken refreshToken) =>
        DbContext.RefreshTokens.Add(refreshToken);

    public Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash) =>
        DbContext.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

    public Task<RefreshToken?> GetRefreshTokenWithUserByHashAsync(string tokenHash) =>
        DbContext.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);
}
