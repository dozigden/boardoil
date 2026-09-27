using System.Text.Json;
using BoardOil.Abstractions.ErrorLogs;
using BoardOil.Abstractions.Jobs;
using BoardOil.Contracts.Jobs;

namespace BoardOil.Services.Jobs;

public sealed class ErrorLogPurgeJobHandler(IErrorLogService errorLogs) : JobHandlerBase<MaintenanceJobPayload>
{
    public override string Type => MaintenanceJobTypes.ErrorLogPurge;

    protected override async Task<JobHandlerResult> HandleTypedAsync(
        JobContext context, MaintenanceJobPayload payload, CancellationToken cancellationToken)
    {
        var result = await errorLogs.PurgeExpiredAsync(cancellationToken);
        if (!result.Success || result.Data is null)
        {
            return JobHandlerResult.Failed(result.Message ?? "Error log purge failed.");
        }

        return JobHandlerResult.Succeeded(JsonSerializer.Serialize(new
        {
            retentionDays = result.Data.RetentionDays,
            cutoffUtc = result.Data.CutoffUtc,
            deletedCount = result.Data.DeletedCount
        }));
    }
}
