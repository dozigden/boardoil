using BoardOil.Persistence.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Persistence.Abstractions.Auth;

public interface IRefreshTokenRepository : IRepositoryBase<EntityRefreshToken>
{
    Task<EntityRefreshToken?> GetByHashAsync(string tokenHash);
    Task<EntityRefreshToken?> GetWithUserByHashAsync(string tokenHash);
}
