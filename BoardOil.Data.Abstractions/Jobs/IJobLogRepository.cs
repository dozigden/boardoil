using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.Jobs;

public interface IJobLogRepository
{
    void Add(EntityJobLog log);
    Task<int> DeleteBatchAsync(int jobId, int batchSize, CancellationToken cancellationToken = default);
}
