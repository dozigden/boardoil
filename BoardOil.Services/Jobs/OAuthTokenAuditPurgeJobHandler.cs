using System.Text.Json;
using BoardOil.Abstractions.Jobs;
using BoardOil.Abstractions.OAuth;
using BoardOil.Contracts.Jobs;

namespace BoardOil.Services.Jobs;

public sealed class OAuthTokenAuditPurgeJobHandler(IOAuthTokenAuditService tokenAudits) : JobHandlerBase<MaintenanceJobPayload>
{
    public override string Type => MaintenanceJobTypes.OAuthTokenAuditPurge;

    protected override async Task<JobHandlerResult> HandleTypedAsync(
        JobContext context, MaintenanceJobPayload payload, CancellationToken cancellationToken)
    {
        var result = await tokenAudits.PurgeExpiredAsync(cancellationToken);
        if (!result.Success || result.Data is null)
        {
            return JobHandlerResult.Failed(result.Message ?? "OAuth token audit purge failed.");
        }

        return JobHandlerResult.Succeeded(JsonSerializer.Serialize(new
        {
            retentionDays = result.Data.RetentionDays,
            cutoffUtc = result.Data.CutoffUtc,
            deletedCount = result.Data.DeletedCount
        }));
    }
}
