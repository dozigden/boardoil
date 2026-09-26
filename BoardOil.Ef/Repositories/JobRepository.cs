using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Jobs;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Ef.Repositories;

public sealed class JobRepository(IAmbientDbContextLocator ambientDbContextLocator)
    : RepositoryBase<EntityJob>(ambientDbContextLocator), IJobRepository
{
    public async Task<IReadOnlyList<EntityJob>> ListAsync(int offset, int limit, CancellationToken cancellationToken = default) =>
        await DbSet.AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        DbSet.AsNoTracking().CountAsync(cancellationToken);

    public Task<int> CountActiveAsync(CancellationToken cancellationToken = default) =>
        DbSet.AsNoTracking()
            .CountAsync(x => x.Status == JobStatus.Pending || x.Status == JobStatus.Running, cancellationToken);

    public Task<EntityJob?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        DbSet.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<EntityJob?> GetWithLogsAsync(int id, CancellationToken cancellationToken = default) =>
        DbSet.AsNoTracking()
            .Include(x => x.Logs.OrderBy(log => log.LoggedAtUtc).ThenBy(log => log.Id))
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<EntityJob?> FindNextDueAsync(DateTime nowUtc, CancellationToken cancellationToken = default) =>
        DbSet.Where(x => x.Status == JobStatus.Pending && x.RunAfterUtc <= nowUtc)
            .OrderBy(x => x.RunAfterUtc)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<EntityJob>> ListRunningForUpdateAsync(CancellationToken cancellationToken = default) =>
        await DbSet.Where(x => x.Status == JobStatus.Running)
            .OrderBy(x => x.StartedAtUtc)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default) =>
        DbSet.AsNoTracking().AnyAsync(x => x.CorrelationId == correlationId, cancellationToken);

    public Task<EntityJob?> GetLatestActiveByCorrelationPrefixesAsync(
        string scheduledPrefix, string adHocPrefix, CancellationToken cancellationToken = default) =>
        DbSet.AsNoTracking()
            .Where(x => x.CorrelationId != null
                && (x.CorrelationId.StartsWith(scheduledPrefix) || x.CorrelationId.StartsWith(adHocPrefix))
                && (x.Status == JobStatus.Pending || x.Status == JobStatus.Running))
            .OrderByDescending(x => x.Status == JobStatus.Running)
            .ThenByDescending(x => x.StartedAtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<EntityJob?> GetLatestStartedByCorrelationPrefixesAsync(
        string scheduledPrefix, string adHocPrefix, CancellationToken cancellationToken = default) =>
        DbSet.AsNoTracking()
            .Where(x => x.CorrelationId != null
                && (x.CorrelationId.StartsWith(scheduledPrefix) || x.CorrelationId.StartsWith(adHocPrefix))
                && x.StartedAtUtc != null)
            .OrderByDescending(x => x.StartedAtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<JobPurgeCandidate?> GetNextExpiredPurgeCandidateAsync(
        DateTime completedBeforeUtc, CancellationToken cancellationToken = default) =>
        DbSet.AsNoTracking()
            .Where(job => (job.Status == JobStatus.Completed || job.Status == JobStatus.Failed || job.Status == JobStatus.Cancelled)
                && job.CompletedAtUtc != null
                && job.CompletedAtUtc < completedBeforeUtc
                && !DbContext.ErrorLogs.Any(errorLog => errorLog.JobId == job.Id))
            .OrderBy(job => job.CompletedAtUtc)
            .ThenBy(job => job.Id)
            .Select(job => new JobPurgeCandidate(job.Id, job.Type, job.Status, job.CompletedAtUtc!.Value))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int> DeleteEmptyExpiredAsync(
        int jobId, DateTime completedBeforeUtc, CancellationToken cancellationToken = default) =>
        DbSet.Where(job => job.Id == jobId
                && (job.Status == JobStatus.Completed || job.Status == JobStatus.Failed || job.Status == JobStatus.Cancelled)
                && job.CompletedAtUtc != null
                && job.CompletedAtUtc < completedBeforeUtc
                && !job.Logs.Any()
                && !DbContext.ErrorLogs.Any(errorLog => errorLog.JobId == job.Id))
            .ExecuteDeleteAsync(cancellationToken);
}
