namespace BoardOil.Services.Jobs;

public sealed class JobLifecycleGate
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await action(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        await ExecuteAsync(async token =>
        {
            await action(token);
            return true;
        }, cancellationToken);
    }
}
