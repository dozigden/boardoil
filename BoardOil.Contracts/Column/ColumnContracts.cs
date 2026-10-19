namespace BoardOil.Contracts.Column;

public sealed record ColumnDto(
    int Id,
    string Title,
    string SortKey,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CreateColumnRequest(
    string Title);

public sealed record UpdateColumnRequest(
    string Title);

public sealed record ReorderColumnsRequest(
    IReadOnlyList<int>? ColumnIds);
