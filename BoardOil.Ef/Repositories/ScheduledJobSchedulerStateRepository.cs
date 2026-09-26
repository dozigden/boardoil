using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Jobs;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class ScheduledJobSchedulerStateRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityScheduledJobSchedulerState>(ambientDbContextLocator), IScheduledJobSchedulerStateRepository
{
    public Task<EntityScheduledJobSchedulerState?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        DbSet.FirstOrDefaultAsync(x => x.Name == name, cancellationToken);

    public async Task<IReadOnlyList<EntityScheduledJobSchedulerState>> ListAsync(CancellationToken cancellationToken = default) =>
        await DbSet.OrderBy(x => x.Name).ToListAsync(cancellationToken);
}
