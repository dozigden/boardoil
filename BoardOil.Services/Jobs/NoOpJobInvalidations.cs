using BoardOil.Abstractions.Jobs;

namespace BoardOil.Services.Jobs;

public sealed class NoOpJobInvalidations : IJobInvalidations
{
    public Task JobChangedAsync(int jobId) => Task.CompletedTask;
    public Task JobHistoryChangedAsync() => Task.CompletedTask;
}
