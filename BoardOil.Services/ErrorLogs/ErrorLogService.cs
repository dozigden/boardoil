using System.Text.RegularExpressions;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.ErrorLogs;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.ErrorLogs;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.ErrorLogs;
using Microsoft.Extensions.Logging;

namespace BoardOil.Services.ErrorLogs;

public sealed class ErrorLogService(
    IDbContextScopeFactory scopeFactory,
    IErrorLogRepository repository,
    TimeProvider timeProvider,
    ILogger<ErrorLogService> logger) : IErrorLogService
{
    private const int DefaultPageSize = 100;
    private const int MaxPageSize = 200;
    private const int SourceMaxLength = 32;
    private const int AreaMaxLength = 64;
    private const int ExceptionTypeMaxLength = 512;
    private const int MessageMaxLength = 2048;
    private const int StackTraceMaxLength = 32768;
    private const int TraceIdentifierMaxLength = 128;
    private const int RequestMethodMaxLength = 16;
    private const int RequestPathMaxLength = 2048;
    private const int ContextJsonMaxLength = 32768;
    private static readonly Regex BearerTokenPattern = new(
        @"(?i)(bearer\s+)[^\s,;]+",
        RegexOptions.CultureInvariant);
    private static readonly Regex SensitiveValuePattern = new(
        @"(?i)(\b(?:token|credential|secret|password|authorization)\s*[=:]\s*)[^\s,;&]+",
        RegexOptions.CultureInvariant);
    private static readonly Regex SensitiveQueryValuePattern = new(
        @"(?i)([?&][^=\s&#]*(?:token|credential|secret|password|authorization)[^=\s&#]*=)[^&#\s]+",
        RegexOptions.CultureInvariant);

    public async Task<ApiResult<ErrorLogListDto>> ListAsync(int? offset, int? limit)
    {
        var listOffset = offset ?? 0;
        var listLimit = limit ?? DefaultPageSize;
        var validationErrors = ValidatePagination(listOffset, listLimit);
        if (validationErrors.Count > 0)
        {
            return ApiErrors.BadRequest("Invalid pagination parameters.", validationErrors);
        }

        using var scope = scopeFactory.CreateReadOnly();
        var totalCount = await repository.CountAsync();
        var errorLogs = await repository.ListAsync(listOffset, listLimit);
        return ApiResults.Ok(new ErrorLogListDto(
            errorLogs.Select(ToDto).ToArray(),
            listOffset,
            listLimit,
            totalCount));
    }

    public async Task<ApiResult<ErrorLogDetailsDto>> GetAsync(int id)
    {
        using var scope = scopeFactory.CreateReadOnly();
        var errorLog = await repository.GetAsync(id);
        if (errorLog is null)
        {
            return ApiErrors.NotFound("Error log not found.");
        }

        return ApiResults.Ok(ToDetailsDto(errorLog));
    }

    public async Task<ApiResult<ErrorLogPurgeResultDto>> PurgeExpiredAsync(
        CancellationToken cancellationToken = default)
    {
        var cutoffUtc = timeProvider
            .GetUtcNow()
            .UtcDateTime
            .AddDays(-ErrorLogRetention.RetentionDays);
        using var scope = scopeFactory.Create();
        var deletedCount = await repository.DeleteOlderThanAsync(cutoffUtc, cancellationToken);
        return ApiResults.Ok(new ErrorLogPurgeResultDto(
            ErrorLogRetention.RetentionDays,
            cutoffUtc,
            deletedCount));
    }

    public async Task<int?> LogExceptionAsync(
        Exception exception,
        ErrorLogContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            using var _ = scopeFactory.SuppressAmbientContext();
            using var scope = scopeFactory.Create(DbContextScopeOption.ForceCreateNew);
            var entity = new EntityErrorLog
            {
                OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
                Source = TruncateRequired(RequireValue(context.Source, nameof(context.Source)), SourceMaxLength),
                Area = TruncateRequired(RequireValue(context.Area, nameof(context.Area)), AreaMaxLength),
                ExceptionType = TruncateRequired(exception.GetType().FullName ?? exception.GetType().Name, ExceptionTypeMaxLength),
                Message = TruncateRequired(RedactRequired(exception.Message), MessageMaxLength),
                StackTrace = Truncate(Redact(exception.ToString()), StackTraceMaxLength),
                TraceIdentifier = Truncate(Redact(context.TraceIdentifier), TraceIdentifierMaxLength),
                RequestMethod = Truncate(Redact(context.RequestMethod), RequestMethodMaxLength),
                RequestPath = Truncate(Redact(context.RequestPath), RequestPathMaxLength),
                ActorUserId = context.ActorUserId,
                ContextJson = Truncate(Redact(context.ContextJson), ContextJsonMaxLength)
            };

            repository.Add(entity);
            await scope.SaveChangesAsync(cancellationToken);
            return entity.Id;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception logException)
        {
            logger.LogError(logException, "Failed to persist exception details to ErrorLogs.");
            return null;
        }
    }

    private static List<ValidationError> ValidatePagination(int offset, int limit)
    {
        var errors = new List<ValidationError>();
        if (offset < 0)
        {
            errors.Add(new ValidationError(nameof(offset), "Offset must be 0 or greater."));
        }

        if (limit < 1 || limit > MaxPageSize)
        {
            errors.Add(new ValidationError(nameof(limit), $"Limit must be between 1 and {MaxPageSize}."));
        }

        return errors;
    }

    private static string RequireValue(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", name);
        }

        return value.Trim();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }

    private static string TruncateRequired(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string RedactRequired(string value) =>
        Redact((string?)value) ?? string.Empty;

    private static string? Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var redacted = BearerTokenPattern.Replace(value, "$1[redacted]");
        redacted = SensitiveQueryValuePattern.Replace(redacted, "$1[redacted]");
        return SensitiveValuePattern.Replace(redacted, "$1[redacted]");
    }

    private static ErrorLogDto ToDto(EntityErrorLog errorLog) =>
        new(
            errorLog.Id,
            errorLog.OccurredAtUtc,
            errorLog.Source,
            errorLog.Area,
            errorLog.ExceptionType,
            errorLog.Message,
            errorLog.TraceIdentifier,
            errorLog.RequestMethod,
            errorLog.RequestPath,
            errorLog.ActorUserId,
            errorLog.CreatedAtUtc,
            errorLog.UpdatedAtUtc);

    private static ErrorLogDetailsDto ToDetailsDto(EntityErrorLog errorLog) =>
        new(
            errorLog.Id,
            errorLog.OccurredAtUtc,
            errorLog.Source,
            errorLog.Area,
            errorLog.ExceptionType,
            errorLog.Message,
            errorLog.StackTrace,
            errorLog.TraceIdentifier,
            errorLog.RequestMethod,
            errorLog.RequestPath,
            errorLog.ActorUserId,
            errorLog.ContextJson,
            errorLog.CreatedAtUtc,
            errorLog.UpdatedAtUtc);
}
