using BoardOil.Abstractions.Entities;
using BoardOil.Abstractions.DataAccess;

namespace BoardOil.Abstractions.Auth;

public interface IRefreshTokenRepository : IRepositoryBase<RefreshToken>
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash);
    Task<RefreshToken?> GetWithUserByHashAsync(string tokenHash);
}
