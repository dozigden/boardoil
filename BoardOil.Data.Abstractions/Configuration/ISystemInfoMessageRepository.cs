using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.Configuration;

public interface ISystemInfoMessageRepository : IRepositoryBase<EntitySystemInfoMessage>
{
    Task<EntitySystemInfoMessage?> GetCurrentAsync();
}
