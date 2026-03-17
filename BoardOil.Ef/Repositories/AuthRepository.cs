using BoardOil.Ef;
using BoardOil.Abstractions.Entities;
using BoardOil.Abstractions.Auth;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class AuthRepository(BoardOilDbContext dbContext) : IAuthRepository
{
    public Task<bool> AnyUsersAsync() =>
        dbContext.Users.AnyAsync();

    public void AddUser(BoardUser user) =>
        dbContext.Users.Add(user);

    public Task<BoardUser?> GetUserByUserNameAsync(string userName) =>
        dbContext.Users.FirstOrDefaultAsync(x => x.UserName == userName);

    public Task<BoardUser?> GetActiveUserByIdAsync(int id) =>
        dbContext.Users.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);

    public void AddRefreshToken(RefreshToken refreshToken) =>
        dbContext.RefreshTokens.Add(refreshToken);

    public Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash) =>
        dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

    public Task<RefreshToken?> GetRefreshTokenWithUserByHashAsync(string tokenHash) =>
        dbContext.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

    public Task SaveChangesAsync() =>
        dbContext.SaveChangesAsync();
}
