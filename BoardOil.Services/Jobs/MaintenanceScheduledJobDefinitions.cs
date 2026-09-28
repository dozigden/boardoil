using BoardOil.Abstractions.Jobs;
using BoardOil.Contracts.Jobs;

namespace BoardOil.Services.Jobs;

public abstract class MaintenanceScheduledJobDefinition(
    string name, string displayName, string jobType, TimeOnly dailyTime) : IScheduledJobDefinition
{
    public string Name => name;
    public string DisplayName => displayName;
    public string SchedulerStateName => $"{name}-checkpoint";

    public Task<ScheduledJobDefinitionConfiguration> GetConfigurationAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ScheduledJobDefinitionConfiguration(true, dailyTime, true));

    public Task<IReadOnlyList<ScheduledJobOccurrence>> CreateOccurrencesAsync(
        DateTime dueAtUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ScheduledJobOccurrence>>([new(jobType)]);
}

public sealed class OAuthClientRegistrationPurgeScheduledJobDefinition()
    : MaintenanceScheduledJobDefinition("oauth-client-registration-purge", "OAuth client registration purge",
        MaintenanceJobTypes.OAuthClientRegistrationPurge, new TimeOnly(3, 0));

public sealed class ErrorLogPurgeScheduledJobDefinition()
    : MaintenanceScheduledJobDefinition("error-log-purge", "Error log purge",
        MaintenanceJobTypes.ErrorLogPurge, new TimeOnly(3, 0));

public sealed class OAuthTokenAuditPurgeScheduledJobDefinition()
    : MaintenanceScheduledJobDefinition("oauth-token-audit-purge", "OAuth token audit purge",
        MaintenanceJobTypes.OAuthTokenAuditPurge, new TimeOnly(3, 0));

public sealed class HistoryPurgeScheduledJobDefinition()
    : MaintenanceScheduledJobDefinition("history-purge", "Job history purge",
        MaintenanceJobTypes.HistoryPurge, new TimeOnly(3, 0));
