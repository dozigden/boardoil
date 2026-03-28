using BoardOil.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Configuration;
using BoardOil.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class AppSettingRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityAppSetting>(ambientDbContextLocator), IAppSettingRepository
{
    public Task<EntityAppSetting?> GetByKeyAsync(string key) =>
        DbSet.FirstOrDefaultAsync(x => x.Key == key);
}
