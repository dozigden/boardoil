using System.Text.Json;
using BoardOil.Abstractions.ErrorLogs;

namespace BoardOil.Api.OAuth;

public sealed class OAuthDynamicClientRegistrationCleanupService(
    IServiceScopeFactory scopeFactory,
    OAuthDynamicClientRegistrationCleanupFailureLogger failureLogger,
    ILogger<OAuthDynamicClientRegistrationCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IOAuthDynamicClientRegistrationService>();
                var deleted = await service.CleanupExpiredRegistrationsAsync(stoppingToken);
                if (deleted > 0)
                {
                    logger.LogInformation("Removed {RegistrationCount} expired OAuth client registrations.", deleted);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "OAuth client registration cleanup failed.");
                await failureLogger.LogAsync(exception);
            }

        }
    }
}

public sealed class OAuthDynamicClientRegistrationCleanupFailureLogger(
    IServiceScopeFactory scopeFactory,
    ILogger<OAuthDynamicClientRegistrationCleanupFailureLogger> logger)
{
    public async Task LogAsync(Exception exception)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var errorLogService = scope.ServiceProvider.GetRequiredService<IErrorLogService>();
            await errorLogService.LogExceptionAsync(
                exception,
                new ErrorLogContext(
                    ErrorLogSources.Backend,
                    ErrorLogAreas.BackgroundService,
                    ContextJson: JsonSerializer.Serialize(new
                    {
                        service = nameof(OAuthDynamicClientRegistrationCleanupService)
                    })),
                CancellationToken.None);
        }
        catch (Exception logException)
        {
            logger.LogError(logException, "Failed to persist OAuth cleanup failure to ErrorLogs.");
        }
    }
}
