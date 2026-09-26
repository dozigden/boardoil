namespace BoardOil.Abstractions.Jobs;

public interface IJobRunner
{
    Task<bool> RunNextDueAsync(CancellationToken cancellationToken = default);
    Task MarkRunningJobsFailedAsync(CancellationToken cancellationToken = default);
}
