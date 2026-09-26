using System.Text.Json;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Jobs;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.Jobs;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Jobs;

namespace BoardOil.Services.Jobs;

public sealed class JobService(
    IDbContextScopeFactory scopes,
    IJobRepository jobs,
    IJobLogRepository logs,
    TimeProvider clock) : IJobService
{
    public async Task<ApiResult<JobListDto>> ListAsync(
        JobListRequest request, CancellationToken cancellationToken = default)
    {
        var errors = new List<ValidationError>();
        if (request.Offset < 0)
        {
            errors.Add(new ValidationError("offset", "Offset must be 0 or greater."));
        }

        if (request.Limit is < 1 or > 200)
        {
            errors.Add(new ValidationError("limit", "Limit must be between 1 and 200."));
        }

        if (errors.Count > 0)
        {
            return ApiErrors.BadRequest("Invalid pagination parameters.", errors);
        }

        using var scope = scopes.CreateReadOnly(DbContextScopeOption.ForceCreateNew);
        var items = await jobs.ListAsync(request.Offset, request.Limit, cancellationToken);
        var count = await jobs.CountAsync(cancellationToken);
        return ApiResults.Ok(new JobListDto(
            items.Select(ToDto).ToArray(), request.Offset, request.Limit, count));
    }

    public async Task<ApiResult<JobDetailsDto>> GetAsync(
        int id, CancellationToken cancellationToken = default)
    {
        using var scope = scopes.CreateReadOnly(DbContextScopeOption.ForceCreateNew);
        var job = await jobs.GetWithLogsAsync(id, cancellationToken);
        if (job is null)
        {
            return ApiErrors.NotFound("Job not found.");
        }

        return ApiResults.Ok(new JobDetailsDto(
            job.Id, job.Type, ToContractStatus(job.Status), job.RunAfterUtc,
            job.PayloadJson, job.ResultJson, job.ErrorMessage, job.StartedAtUtc,
            job.CompletedAtUtc, job.UserId, job.CorrelationId, job.CreatedAtUtc,
            job.UpdatedAtUtc, job.Logs.Select(ToLogDto).ToArray()));
    }

    public async Task<int> CountActiveAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopes.CreateReadOnly(DbContextScopeOption.ForceCreateNew);
        return await jobs.CountActiveAsync(cancellationToken);
    }

    public async Task<ApiResult<JobDto>> EnqueueAsync(
        CreateJobRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return ApiErrors.BadRequest("Invalid job request.", errors);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var job = new EntityJob
        {
            Type = request.Type.Trim(),
            Status = JobStatus.Pending,
            RunAfterUtc = request.RunAfterUtc?.ToUniversalTime() ?? now,
            PayloadJson = string.IsNullOrWhiteSpace(request.PayloadJson) ? "{}" : request.PayloadJson.Trim(),
            ResultJson = "{}",
            UserId = request.UserId,
            CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? null : request.CorrelationId.Trim()
        };
        using var scope = scopes.Create(DbContextScopeOption.ForceCreateNew);
        jobs.Add(job);
        logs.Add(new EntityJobLog
        {
            Job = job,
            Level = JobLogLevel.Info,
            Message = "Job enqueued.",
            DataJson = JsonSerializer.Serialize(new { job.Type }),
            LoggedAtUtc = now
        });
        await scope.SaveChangesAsync(cancellationToken);
        return ApiResults.Created(ToDto(job));
    }

    public static JobDto ToDto(EntityJob job) =>
        new(job.Id, job.Type, ToContractStatus(job.Status), job.RunAfterUtc,
            job.PayloadJson, job.ResultJson, job.ErrorMessage, job.StartedAtUtc,
            job.CompletedAtUtc, job.UserId, job.CorrelationId,
            job.CreatedAtUtc, job.UpdatedAtUtc);

    public static string ToContractStatus(JobStatus status) => status switch
    {
        JobStatus.Pending => JobStatuses.Pending,
        JobStatus.Running => JobStatuses.Running,
        JobStatus.Completed => JobStatuses.Completed,
        JobStatus.Failed => JobStatuses.Failed,
        JobStatus.Cancelled => JobStatuses.Cancelled,
        _ => throw new InvalidOperationException($"Unsupported job status: {status}")
    };

    public static string ToContractLogLevel(JobLogLevel level) => level switch
    {
        JobLogLevel.Info => JobLogLevels.Info,
        JobLogLevel.Warning => JobLogLevels.Warning,
        JobLogLevel.Error => JobLogLevels.Error,
        _ => throw new InvalidOperationException($"Unsupported job log level: {level}")
    };

    private static JobLogDto ToLogDto(EntityJobLog log) =>
        new(log.Id, ToContractLogLevel(log.Level), log.Message, log.DataJson, log.LoggedAtUtc);

    private static List<ValidationError> Validate(CreateJobRequest request)
    {
        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(request.Type))
        {
            errors.Add(new ValidationError("type", "Job type is required."));
        }
        else if (request.Type.Trim().Length > 200)
        {
            errors.Add(new ValidationError("type", "Job type must be at most 200 characters."));
        }

        if (!string.IsNullOrWhiteSpace(request.PayloadJson))
        {
            try
            {
                using var document = JsonDocument.Parse(request.PayloadJson);
            }
            catch (JsonException)
            {
                errors.Add(new ValidationError("payloadJson", "Payload JSON must be valid JSON."));
            }
        }

        if (request.CorrelationId?.Trim().Length > 128)
        {
            errors.Add(new ValidationError("correlationId", "Correlation id must be at most 128 characters."));
        }

        return errors;
    }
}
