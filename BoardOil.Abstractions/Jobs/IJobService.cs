using BoardOil.Contracts.Common;
using BoardOil.Contracts.Jobs;

namespace BoardOil.Abstractions.Jobs;

public interface IJobService
{
    Task<ApiResult<JobListDto>> ListAsync(JobListRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<JobDetailsDto>> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CountActiveAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<JobDto>> EnqueueAsync(CreateJobRequest request, CancellationToken cancellationToken = default);
}

public sealed record CreateJobRequest(
    string Type,
    string? PayloadJson = null,
    DateTime? RunAfterUtc = null,
    int? UserId = null,
    string? CorrelationId = null);
