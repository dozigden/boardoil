namespace BoardOil.Services.Column;

public sealed record ColumnDto(
    int Id,
    string Title,
    int Position,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CreateColumnRequest(
    string Title,
    int? Position);

public sealed record UpdateColumnRequest(
    string? Title,
    int? Position);
