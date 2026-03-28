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
    int BoardId,
    string BoardName,
    DateTime UpdatedAtUtc,
    IReadOnlyList<McpColumnSnapshot> Columns);

public sealed record McpColumnSnapshot(
    int ColumnId,
    string Title,
    string SortKey,
    IReadOnlyList<McpCardSnapshot> Cards);

public sealed record McpCardSnapshot(
    int CardId,
    int BoardColumnId,
    string Title,
    string Description,
    string SortKey,
    IReadOnlyList<string> TagNames,
    DateTime UpdatedAtUtc);

public sealed record BoardGetInput(int BoardId);

public sealed record ColumnsListInput(int BoardId);

public sealed record ColumnsListOutput(
    int BoardId,
    IReadOnlyList<McpColumnReference> Columns);

public sealed record McpColumnReference(
    int ColumnId,
    string Title,
    string SortKey);

public sealed record CardCreateInput(
    int BoardId,
    int BoardColumnId,
    string Title,
    string Description,
    IReadOnlyList<string>? TagNames);

public sealed record CardUpdateInput(
    int BoardId,
    int CardId,
    string Title,
    string Description,
    IReadOnlyList<string> TagNames);

public sealed record CardMoveInput(
    int BoardId,
    int CardId,
    int BoardColumnId,
    int? PositionAfterCardId);

public sealed record CardMoveByColumnNameInput(
    int BoardId,
    int CardId,
    string ColumnTitle,
    int? PositionAfterCardId);

public sealed record CardDeleteInput(
    int BoardId,
    int CardId);

public sealed record CardMutationOutput(
    McpCardSnapshot? Card,
    string Outcome);
