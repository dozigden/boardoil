using System.Text.Json;
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
    private const int ClientPhaseMaxLength = 64;
    private const int ClientRouteNameMaxLength = 256;
    private const int ClientFrontendVersionMaxLength = 512;
    private const int ClientUserAgentMaxLength = 2048;
    private const int ClientContextRawMaxLength = 16384;
    private const int ClientContextValueMaxLength = 2048;
    private const int ClientContextKeyMaxLength = 64;
    private const int ClientContextMaxKeys = 20;
    private const int ClientContextMaxDepth = 2;
    private const int ClientViewportMaxDimension = 100000;
    private const string DefaultClientExceptionType = "FrontendError";
    private static readonly Regex BearerTokenPattern = new(
        @"(?i)(bearer\s+)[^\s,;]+",
        RegexOptions.CultureInvariant);
    private static readonly Regex SensitiveValuePattern = new(
        @"(?i)(\b(?:token|credential|secret|password|authorization|cookie)\s*[=:]\s*)[^\s,;&]+",
        RegexOptions.CultureInvariant);
    private static readonly Regex SensitiveQueryValuePattern = new(
        @"(?i)([?&][^=\s&#]*(?:token|credential|secret|password|authorization|cookie)[^=\s&#]*=)[^&#\s]+",
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

    public async Task<ApiResult<ErrorLogDto>> ReportClientErrorAsync(
        ClientErrorReportRequest request,
        int actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationErrors = ValidateClientError(request);
        if (validationErrors.Count > 0)
        {
            return ApiErrors.BadRequest("Invalid frontend error report.", validationErrors);
        }

        try
        {
            using var _ = scopeFactory.SuppressAmbientContext();
            using var scope = scopeFactory.Create(DbContextScopeOption.ForceCreateNew);
            var entity = new EntityErrorLog
            {
                OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
                Source = ErrorLogSources.Frontend,
                Area = ErrorLogAreas.WebClient,
                ExceptionType = TruncateRequired(
                    RedactRequired(NormaliseOptional(request.ExceptionType) ?? DefaultClientExceptionType),
                    ExceptionTypeMaxLength),
                Message = TruncateRequired(RedactRequired(request.Message.Trim()), MessageMaxLength),
                StackTrace = Truncate(Redact(request.StackTrace), StackTraceMaxLength),
                RequestPath = Truncate(
                    Redact(NormaliseOptional(request.RoutePath)),
                    RequestPathMaxLength),
                ActorUserId = actorUserId,
                ContextJson = BuildClientContextJson(request)
            };

            repository.Add(entity);
            await scope.SaveChangesAsync(cancellationToken);
            return ApiResults.Ok(ToDto(entity));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception logException)
        {
            logger.LogError(logException, "Failed to persist frontend exception details to ErrorLogs.");
            return ApiErrors.InternalError("Frontend error report could not be logged.");
        }
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
                ContextJson = BuildBackendContextJson(context.ContextJson)
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

    private static List<ValidationError> ValidateClientError(ClientErrorReportRequest request)
    {
        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            errors.Add(new ValidationError("message", "Message is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Phase))
        {
            errors.Add(new ValidationError("phase", "Phase is required."));
        }

        if (request.Viewport is { } viewport
            && (viewport.Width < 0
                || viewport.Height < 0
                || viewport.Width > ClientViewportMaxDimension
                || viewport.Height > ClientViewportMaxDimension))
        {
            errors.Add(new ValidationError("viewport", "Viewport dimensions are invalid."));
        }

        if (request.Context is { } context
            && context.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            if (context.ValueKind != JsonValueKind.Object)
            {
                errors.Add(new ValidationError("context", "Context must be a JSON object."));
            }
            else if (context.GetRawText().Length > ClientContextRawMaxLength)
            {
                errors.Add(new ValidationError("context", "Context is too large."));
            }
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

    private static string? NormaliseOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string BuildClientContextJson(ClientErrorReportRequest request)
    {
        var context = new
        {
            phase = Truncate(Redact(request.Phase.Trim()), ClientPhaseMaxLength),
            routeName = Truncate(Redact(NormaliseOptional(request.RouteName)), ClientRouteNameMaxLength),
            routePath = Truncate(
                Redact(NormaliseOptional(request.RoutePath)),
                RequestPathMaxLength),
            frontendVersion = Truncate(
                Redact(NormaliseOptional(request.FrontendVersion)),
                ClientFrontendVersionMaxLength),
            viewport = request.Viewport,
            userAgent = Truncate(Redact(NormaliseOptional(request.UserAgent)), ClientUserAgentMaxLength),
            clientContext = SanitiseClientContext(request.Context)
        };

        var serialised = JsonSerializer.Serialize(context);
        if (serialised.Length <= ContextJsonMaxLength)
        {
            return serialised;
        }

        return JsonSerializer.Serialize(new
        {
            context.phase,
            context.routeName,
            context.routePath,
            context.frontendVersion,
            context.viewport,
            context.userAgent,
            clientContext = (object?)null
        });
    }

    private static string? BuildBackendContextJson(string? contextJson)
    {
        if (string.IsNullOrWhiteSpace(contextJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(contextJson);
            var sanitised = SanitiseJsonValue(document.RootElement, 0);
            var serialised = JsonSerializer.Serialize(sanitised);
            if (serialised.Length <= ContextJsonMaxLength)
            {
                return serialised;
            }

            return JsonSerializer.Serialize(new { contextTruncated = true });
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(new { contextInvalid = true });
        }
    }

    private static object? SanitiseClientContext(JsonElement? context)
    {
        if (context is not { ValueKind: JsonValueKind.Object } value)
        {
            return null;
        }

        return SanitiseJsonObject(value, 0);
    }

    private static Dictionary<string, object?> SanitiseJsonObject(JsonElement value, int depth)
    {
        var sanitised = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (depth >= ClientContextMaxDepth)
        {
            return sanitised;
        }

        foreach (var property in value.EnumerateObject())
        {
            if (sanitised.Count >= ClientContextMaxKeys)
            {
                break;
            }

            if (IsSensitiveContextKey(property.Name))
            {
                continue;
            }

            var safeKey = TruncateRequired(
                RedactRequired(property.Name),
                ClientContextKeyMaxLength);
            sanitised[safeKey] = SanitiseJsonValue(property.Value, depth + 1);
        }

        return sanitised;
    }

    private static object? SanitiseJsonValue(JsonElement value, int depth)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Object when depth < ClientContextMaxDepth => SanitiseJsonObject(value, depth),
            JsonValueKind.Array when depth < ClientContextMaxDepth => value
                .EnumerateArray()
                .Take(ClientContextMaxKeys)
                .Select(item => SanitiseJsonValue(item, depth + 1))
                .ToArray(),
            JsonValueKind.String => Truncate(Redact(value.GetString()), ClientContextValueMaxLength),
            JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static bool IsSensitiveContextKey(string key) =>
        key.Contains("token", StringComparison.OrdinalIgnoreCase)
        || key.Contains("credential", StringComparison.OrdinalIgnoreCase)
        || key.Contains("secret", StringComparison.OrdinalIgnoreCase)
        || key.Contains("password", StringComparison.OrdinalIgnoreCase)
        || key.Contains("authorization", StringComparison.OrdinalIgnoreCase)
        || key.Contains("cookie", StringComparison.OrdinalIgnoreCase)
        || key.Contains("content", StringComparison.OrdinalIgnoreCase);

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
