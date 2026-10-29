namespace BoardOil.Data.Abstractions.ErrorLogs;

public sealed record ErrorLogFilter(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? Source = null,
    string? Area = null,
    string? Message = null,
    string? TraceIdentifier = null);
