namespace BoardOil.Contracts.Jobs;

public static class JobStatuses
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

public static class JobLogLevels
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Error = "error";
}

public sealed record JobListRequest(int Offset = 0, int Limit = 100);

public sealed record JobDto(
    int Id, string Type, string Status, DateTime RunAfterUtc, string PayloadJson,
    string ResultJson, string? ErrorMessage, DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc, int? UserId, string? CorrelationId,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public sealed record JobLogDto(
    int Id, string Level, string Message, string? DataJson, DateTime LoggedAtUtc);

public sealed record JobDetailsDto(
    int Id, string Type, string Status, DateTime RunAfterUtc, string PayloadJson,
    string ResultJson, string? ErrorMessage, DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc, int? UserId, string? CorrelationId,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc, IReadOnlyList<JobLogDto> Logs);

public sealed record JobListDto(
    IReadOnlyList<JobDto> Items, int Offset, int Limit, int TotalCount);
