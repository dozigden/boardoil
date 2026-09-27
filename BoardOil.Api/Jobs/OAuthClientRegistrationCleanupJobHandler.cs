using System.Text.Json;
using BoardOil.Abstractions.Jobs;
using BoardOil.Api.OAuth;
using BoardOil.Contracts.Jobs;

namespace BoardOil.Api.Jobs;

public sealed class OAuthClientRegistrationCleanupJobHandler(
    IOAuthDynamicClientRegistrationService registrations) : JobHandlerBase<MaintenanceJobPayload>
{
    public override string Type => MaintenanceJobTypes.OAuthClientRegistrationCleanup;

    protected override async Task<JobHandlerResult> HandleTypedAsync(
        JobContext context, MaintenanceJobPayload payload, CancellationToken cancellationToken)
    {
        var deletedCount = await registrations.CleanupExpiredRegistrationsAsync(cancellationToken);
        return JobHandlerResult.Succeeded(JsonSerializer.Serialize(new { deletedCount }));
    }
}
