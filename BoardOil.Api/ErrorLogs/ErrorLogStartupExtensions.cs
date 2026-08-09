using BoardOil.Abstractions.ErrorLogs;

namespace BoardOil.Api.ErrorLogs;

public static class ErrorLogStartupExtensions
{
    public static async Task PurgeExpiredErrorLogsAsync(this IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("BoardOil.ErrorLogStartup");

        try
        {
            var service = scope.ServiceProvider.GetRequiredService<IErrorLogService>();
            var result = await service.PurgeExpiredAsync();
            if (result.Success && result.Data is not null && result.Data.DeletedCount > 0)
            {
                logger.LogInformation(
                    "Purged {DeletedCount} error logs older than {RetentionDays} days during startup.",
                    result.Data.DeletedCount,
                    result.Data.RetentionDays);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to purge expired error logs during startup.");
        }
    }
}
