namespace BoardOil.Api.Realtime;

public sealed class TypingPresenceExpiryService(IServiceProvider serviceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                var presenceService = scope.ServiceProvider.GetRequiredService<ITypingPresenceService>();
                await presenceService.SweepExpiredAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown.
        }
    }
}
