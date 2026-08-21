using BoardOil.Abstractions.OAuth;

namespace BoardOil.Api.OAuth;

public static class OAuthTokenAuditStartupExtensions
{
    public static async Task PurgeExpiredOAuthTokenAuditsAsync(this IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("BoardOil.OAuthTokenAuditStartup");

        try
        {
            var service = scope.ServiceProvider.GetRequiredService<IOAuthTokenAuditService>();
            var result = await service.PurgeExpiredAsync();
            if (result.Success && result.Data is not null && result.Data.DeletedCount > 0)
            {
                logger.LogInformation(
                    "Purged {DeletedCount} OAuth token audit events older than {RetentionDays} days during startup.",
                    result.Data.DeletedCount,
                    result.Data.RetentionDays);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to purge expired OAuth token audit events during startup.");
        }
    }
}
