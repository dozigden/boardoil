using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Jobs;
using BoardOil.Contracts.Jobs;
using BoardOil.Data.Abstractions.Board;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Services.Jobs;

public sealed class QueueSoftDeletePurgesJobHandler(
    IBoardRepository boards,
    IJobService jobs,
    IDbContextScopeFactory scopes,
    TimeProvider timeProvider) : JobHandlerBase<MaintenanceJobPayload>
{
    public override string Type => MaintenanceJobTypes.DeletedBoardPurge;

    protected override async Task<JobHandlerResult> HandleTypedAsync(
        JobContext context, MaintenanceJobPayload payload, CancellationToken cancellationToken)
    {
        var cutoffUtc = timeProvider.GetUtcNow().UtcDateTime.AddDays(-PurgeBoardJobHandler.RetentionDays);
        IReadOnlyList<int> candidates;
        using (scopes.CreateReadOnly())
        {
            candidates = await boards.Query()
                .Where(board => board.DeletedAtUtc <= cutoffUtc)
                .OrderBy(board => board.Id)
                .Select(board => board.Id)
                .ToListAsync(cancellationToken);
        }

        var queuedCount = 0;
        foreach (var boardId in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await jobs.EnqueueAsync(new CreateJobRequest(
                PurgeBoardJobHandler.JobType,
                JsonSerializer.Serialize(new PurgeBoardJobPayload(boardId)),
                CorrelationId: FormattableString.Invariant($"purge-board:{context.JobId}:{boardId}")),
                cancellationToken);
            if (!result.Success)
            {
                return JobHandlerResult.Failed($"Queued {queuedCount} board purge job(s). Could not queue board {boardId}. {result.Message}");
            }
            queuedCount++;
        }

        return JobHandlerResult.Succeeded(JsonSerializer.Serialize(new
        {
            retentionDays = PurgeBoardJobHandler.RetentionDays,
            cutoffUtc,
            queuedCount
        }));
    }
}
