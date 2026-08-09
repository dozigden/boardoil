using System.Text.Json;

namespace BoardOil.Contracts.ErrorLogs;

public sealed record ClientErrorReportRequest(
    string Message,
    string? ExceptionType,
    string? StackTrace,
    string Phase,
    string? RouteName,
    string? RoutePath,
    string? FrontendVersion,
    ClientErrorViewportDto? Viewport,
    string? UserAgent,
    JsonElement? Context);

public sealed record ClientErrorViewportDto(
    int Width,
    int Height);

public sealed record ErrorLogDto(
    int Id,
    DateTime OccurredAtUtc,
    string Source,
    string Area,
    string ExceptionType,
    string Message,
    string? TraceIdentifier,
    string? RequestMethod,
    string? RequestPath,
    int? ActorUserId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record ErrorLogDetailsDto(
    int Id,
    DateTime OccurredAtUtc,
    string Source,
    string Area,
    string ExceptionType,
    string Message,
    string? StackTrace,
    string? TraceIdentifier,
    string? RequestMethod,
    string? RequestPath,
    int? ActorUserId,
    string? ContextJson,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record ErrorLogListDto(
    IReadOnlyList<ErrorLogDto> Items,
    int Offset,
    int Limit,
    int TotalCount);

public sealed record ErrorLogPurgeResultDto(
    int RetentionDays,
    DateTime CutoffUtc,
    int DeletedCount);
