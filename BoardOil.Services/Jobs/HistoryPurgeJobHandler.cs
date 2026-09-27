using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Jobs;
using BoardOil.Contracts.Jobs;
using BoardOil.Data.Abstractions.Jobs;
using Microsoft.Extensions.Logging;

namespace BoardOil.Services.Jobs;

public sealed class HistoryPurgeJobHandler(
    IDbContextScopeFactory scopes,
    IJobRepository jobs,
    IJobLogRepository logs,
    TimeProvider clock,
    ILogger<HistoryPurgeJobHandler> logger) : JobHandlerBase<MaintenanceJobPayload>
{
    public const int RetentionDays = 14;
    public const int DetailBatchSize = 1_000;

    public override string Type => MaintenanceJobTypes.HistoryPurge;

    protected override async Task<JobHandlerResult> HandleTypedAsync(
        JobContext context, MaintenanceJobPayload payload, CancellationToken cancellationToken)
    {
        var cutoffUtc = clock.GetUtcNow().UtcDateTime.AddDays(-RetentionDays);
        var deletedJobCount = 0;
        var deletedLogCount = 0;
        var batchCount = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JobPurgeCandidate? candidate;
            using (scopes.CreateReadOnly(DbContextScopeOption.ForceCreateNew))
            {
                candidate = await jobs.GetNextExpiredPurgeCandidateAsync(cutoffUtc, cancellationToken);
            }

            if (candidate is null)
            {
                break;
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int deleted;
                // ExecuteDelete commits immediately; there is no scope SaveChangesAsync here.
                using (scopes.Create(DbContextScopeOption.ForceCreateNew))
                {
                    deleted = await logs.DeleteBatchAsync(candidate.Id, DetailBatchSize, cancellationToken);
                }

                if (deleted == 0)
                {
                    break;
                }

                deletedLogCount += deleted;
                batchCount++;
            }

            cancellationToken.ThrowIfCancellationRequested();
            // This predicate rechecks status, cutoff, error references and empty logs.
            using (scopes.Create(DbContextScopeOption.ForceCreateNew))
            {
                deletedJobCount += await jobs.DeleteEmptyExpiredAsync(
                    candidate.Id, cutoffUtc, cancellationToken);
            }
        }

        logger.LogInformation(
            "Job history purge removed {DeletedJobCount} jobs and {DeletedLogCount} logs in {BatchCount} batches before {CutoffUtc}.",
            deletedJobCount, deletedLogCount, batchCount, cutoffUtc);
        return JobHandlerResult.Succeeded(JsonSerializer.Serialize(new
        {
            retentionDays = RetentionDays,
            cutoffUtc,
            batchSize = DetailBatchSize,
            deletedJobCount,
            deletedLogCount,
            batchCount
        }));
    }
}
