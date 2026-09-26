using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Jobs;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class JobLogRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityJobLog>(ambientDbContextLocator), IJobLogRepository
{
    public async Task<int> DeleteBatchAsync(int jobId, int batchSize, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

        var logIds = await DbSet.AsNoTracking()
            .Where(log => log.JobId == jobId)
            .OrderBy(log => log.Id)
            .Select(log => log.Id)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);
        if (logIds.Length == 0)
        {
            return 0;
        }

        var upperLogId = logIds[^1];
        return await DbSet.Where(log => log.JobId == jobId && log.Id <= upperLogId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
