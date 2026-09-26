using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.Jobs;

public interface IJobRepository : IRepositoryBase<EntityJob>
{
    Task<IReadOnlyList<EntityJob>> ListAsync(int offset, int limit, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<int> CountActiveAsync(CancellationToken cancellationToken = default);
    Task<EntityJob?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default);
    Task<EntityJob?> GetWithLogsAsync(int id, CancellationToken cancellationToken = default);
    Task<EntityJob?> FindNextDueAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EntityJob>> ListRunningForUpdateAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
    Task<EntityJob?> GetLatestActiveByCorrelationPrefixesAsync(
        string scheduledPrefix, string adHocPrefix, CancellationToken cancellationToken = default);
    Task<EntityJob?> GetLatestStartedByCorrelationPrefixesAsync(
        string scheduledPrefix, string adHocPrefix, CancellationToken cancellationToken = default);
    Task<JobPurgeCandidate?> GetNextExpiredPurgeCandidateAsync(
        DateTime completedBeforeUtc, CancellationToken cancellationToken = default);
    Task<int> DeleteEmptyExpiredAsync(
        int jobId, DateTime completedBeforeUtc, CancellationToken cancellationToken = default);
}

public sealed record JobPurgeCandidate(int Id, string Type, JobStatus Status, DateTime CompletedAtUtc);
