using System.Text.Json;
using BoardOil.Abstractions.Jobs;
using BoardOil.Api.OAuth;
using BoardOil.Contracts.Jobs;

namespace BoardOil.Api.Jobs;

public sealed class OAuthClientRegistrationPurgeJobHandler(
    IOAuthDynamicClientRegistrationService registrations) : JobHandlerBase<MaintenanceJobPayload>
{
    public override string Type => MaintenanceJobTypes.OAuthClientRegistrationPurge;

    protected override async Task<JobHandlerResult> HandleTypedAsync(
        JobContext context, MaintenanceJobPayload payload, CancellationToken cancellationToken)
    {
        var deletedCount = await registrations.PurgeExpiredRegistrationsAsync(cancellationToken);
        return JobHandlerResult.Succeeded(JsonSerializer.Serialize(new { deletedCount }));
    }
}
