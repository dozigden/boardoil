using BoardOil.Persistence.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Persistence.Abstractions.Auth;

public interface IPersonalAccessTokenRepository : IRepositoryBase<EntityPersonalAccessToken>
{
    Task<IReadOnlyList<EntityPersonalAccessToken>> GetByUserIdAsync(int userId);
    Task<EntityPersonalAccessToken?> GetByIdAsync(int id);
    Task<EntityPersonalAccessToken?> GetWithUserByHashAsync(string tokenHash);
}
