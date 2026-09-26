using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.Jobs;

public interface IScheduledJobSchedulerStateRepository : IRepositoryBase<EntityScheduledJobSchedulerState>
{
    Task<EntityScheduledJobSchedulerState?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EntityScheduledJobSchedulerState>> ListAsync(CancellationToken cancellationToken = default);
}
