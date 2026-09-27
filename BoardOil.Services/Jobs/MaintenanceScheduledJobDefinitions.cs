using BoardOil.Abstractions.Jobs;
using BoardOil.Contracts.Jobs;

namespace BoardOil.Services.Jobs;

public abstract class MaintenanceScheduledJobDefinition(
    string name, string displayName, string jobType, string cronExpression) : IScheduledJobDefinition
{
    public string Name => name;
    public string DisplayName => displayName;
    public string SchedulerStateName => $"{name}-checkpoint";

    public Task<ScheduledJobDefinitionConfiguration> GetConfigurationAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ScheduledJobDefinitionConfiguration(true, cronExpression, true));

    public Task<IReadOnlyList<ScheduledJobOccurrence>> CreateOccurrencesAsync(
        DateTime dueAtUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ScheduledJobOccurrence>>([new(jobType)]);
}

public sealed class OAuthClientRegistrationCleanupScheduledJobDefinition()
    : MaintenanceScheduledJobDefinition("oauth-client-registration-cleanup", "OAuth client registration cleanup",
        MaintenanceJobTypes.OAuthClientRegistrationCleanup, "0 3 * * *");

public sealed class ErrorLogPurgeScheduledJobDefinition()
    : MaintenanceScheduledJobDefinition("error-log-purge", "Error log purge",
        MaintenanceJobTypes.ErrorLogPurge, "0 3 * * *");

public sealed class OAuthTokenAuditPurgeScheduledJobDefinition()
    : MaintenanceScheduledJobDefinition("oauth-token-audit-purge", "OAuth token audit purge",
        MaintenanceJobTypes.OAuthTokenAuditPurge, "0 3 * * *");

public sealed class HistoryPurgeScheduledJobDefinition()
    : MaintenanceScheduledJobDefinition("history-purge", "Job history purge",
        MaintenanceJobTypes.HistoryPurge, "0 3 * * *");
