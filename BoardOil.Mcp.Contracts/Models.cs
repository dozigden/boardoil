namespace BoardOil.Mcp.Contracts;

public sealed record McpToolDefinition(
    string Name,
    string Description,
    string InputSchemaJson,
    string OutputSchemaJson);

public sealed record McpToolError(
    string Code,
    string Message,
    int StatusCode,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? ValidationErrors = null);

public sealed record McpToolResult<T>(
    bool Success,
    T? Data,
    McpToolError? Error);

public sealed record McpBoardSnapshot(
    int Id,
    string BoardName,
    DateTime UpdatedAtUtc,
    IReadOnlyList<McpColumnSnapshot> Columns);

public sealed record McpColumnSnapshot(
    int Id,
    string Title,
    string SortKey,
    IReadOnlyList<McpCardSnapshot> Cards);

public sealed record McpCardSnapshot(
    int Id,
    int ColumnId,
    string Title,
    string Description,
    string SortKey,
    IReadOnlyList<string> TagNames,
    DateTime UpdatedAtUtc);

public sealed record BoardGetInput
{
    public int? Id { get; init; }
}

public sealed record ColumnsListInput
{
    public int? Id { get; init; }
}

public sealed record ColumnsListOutput(
    int Id,
    IReadOnlyList<McpColumnReference> Columns);

public sealed record McpColumnReference(
    int Id,
    string Title,
    string SortKey);

public sealed record CardCreateInput
{
    public int? BoardId { get; init; }
    public int? ColumnId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string>? TagNames { get; init; }
}

public sealed record CardUpdateInput
{
    public int? BoardId { get; init; }
    public int? Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string> TagNames { get; init; } = [];
}

public sealed record CardMoveInput
{
    public int? BoardId { get; init; }
    public int? Id { get; init; }
    public int? ColumnId { get; init; }
    public int? AfterId { get; init; }
}

public sealed record CardDeleteInput
{
    public int? BoardId { get; init; }
    public int? Id { get; init; }
}

public sealed record CardMutationOutput(
    McpCardSnapshot? Card,
    string Outcome);
