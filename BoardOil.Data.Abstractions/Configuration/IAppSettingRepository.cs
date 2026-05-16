using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.Configuration;

public interface IAppSettingRepository : IRepositoryBase<EntityAppSetting>
{
    Task<EntityAppSetting?> GetByKeyAsync(string key);
}
