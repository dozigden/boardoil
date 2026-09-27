using BoardOil.Abstractions.Jobs;
using Microsoft.AspNetCore.SignalR;

namespace BoardOil.Api.Realtime;

public sealed class SystemJobInvalidations(
    IHubContext<SystemJobsHub> hub,
    ILogger<SystemJobInvalidations> logger) : IJobInvalidations
{
    public Task JobChangedAsync(int jobId) => PublishAsync(jobId);

    public Task JobHistoryChangedAsync() => PublishAsync(null);

    private async Task PublishAsync(int? jobId)
    {
        try
        {
            await hub.Clients.All.SendAsync("JobsChanged", jobId);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not publish system job invalidation for {JobId}.", jobId);
        }
    }
}
