namespace BoardOil.Abstractions.ErrorLogs;

public sealed record ErrorLogContext(
    string Source,
    string Area,
    string? TraceIdentifier = null,
    string? RequestMethod = null,
    string? RequestPath = null,
    int? ActorUserId = null,
    string? ContextJson = null);
