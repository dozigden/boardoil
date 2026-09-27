namespace BoardOil.Abstractions.Jobs;

/// <summary>Signals that committed job history should be read again.</summary>
public interface IJobInvalidations
{
    Task JobChangedAsync(int jobId);
    Task JobHistoryChangedAsync();
}
