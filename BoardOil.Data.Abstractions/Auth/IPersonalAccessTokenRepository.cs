using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.Auth;

public interface IPersonalAccessTokenRepository : IRepositoryBase<EntityPersonalAccessToken>
{
    Task<IReadOnlyList<EntityPersonalAccessToken>> GetByUserIdAsync(int userId);
    Task<EntityPersonalAccessToken?> GetByIdAsync(int id);
    Task<EntityPersonalAccessToken?> GetWithUserByHashAsync(string tokenHash);
}
