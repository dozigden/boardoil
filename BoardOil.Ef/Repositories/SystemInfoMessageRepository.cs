using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Configuration;
using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class SystemInfoMessageRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntitySystemInfoMessage>(ambientDbContextLocator), ISystemInfoMessageRepository
{
    public Task<EntitySystemInfoMessage?> GetCurrentAsync() =>
        DbSet
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync();
}
