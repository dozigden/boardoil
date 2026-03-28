using BoardOil.Persistence.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Persistence.Abstractions.Configuration;

public interface IAppSettingRepository : IRepositoryBase<EntityAppSetting>
{
    Task<EntityAppSetting?> GetByKeyAsync(string key);
}
