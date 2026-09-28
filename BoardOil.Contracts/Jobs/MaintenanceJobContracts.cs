namespace BoardOil.Contracts.Jobs;

public static class MaintenanceJobTypes
{
    public const string OAuthClientRegistrationPurge = "oauth.client-registration.purge";
    public const string ErrorLogPurge = "error-log.purge";
    public const string OAuthTokenAuditPurge = "oauth-token-audit.purge";
    public const string HistoryPurge = "history.purge";
}

public sealed record MaintenanceJobPayload;
