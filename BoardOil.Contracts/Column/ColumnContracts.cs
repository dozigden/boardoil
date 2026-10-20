using System.Text.Json.Serialization;

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

public sealed record EditColumnsRequest(
    IReadOnlyList<ColumnEditEntry?>? Columns);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ColumnEditEntry(int? Id, string? Title);
