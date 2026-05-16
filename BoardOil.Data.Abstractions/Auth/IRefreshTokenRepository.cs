using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.Auth;

public interface IRefreshTokenRepository : IRepositoryBase<EntityRefreshToken>
{
    Task<EntityRefreshToken?> GetByHashAsync(string tokenHash);
    Task<EntityRefreshToken?> GetWithUserByHashAsync(string tokenHash);
    Task RevokeActiveTokensByUserIdAsync(int userId, DateTime revokedAtUtc);
}
