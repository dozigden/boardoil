using System.Text.Json.Serialization;

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
    string Description,
    DateTime UpdatedAtUtc,
    IReadOnlyList<McpColumnSnapshot> Columns);

public sealed record McpBoardSummary(
    int Id,
    string Name,
    string Description,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? CurrentUserRole);

public sealed record McpColumnSnapshot(
    int Id,
    string Title,
    string SortKey,
    IReadOnlyList<McpBoardCardSnapshot> Cards);

public sealed record McpBoardCardSnapshot(
    int Id,
    int ColumnId,
    int CardTypeId,
    string CardTypeName,
    string? CardTypeEmoji,
    string Title,
    string SortKey,
    IReadOnlyList<McpCardTagSnapshot> Tags,
    IReadOnlyList<string> TagNames,
    DateTime UpdatedAtUtc,
    int? AssignedUserId,
    string? AssignedUserName);

public sealed record McpCardSnapshot(
    int Id,
    int ColumnId,
    int CardTypeId,
    string CardTypeName,
    string? CardTypeEmoji,
    string Title,
    string Description,
    string SortKey,
    IReadOnlyList<McpCardTagSnapshot> Tags,
    IReadOnlyList<string> TagNames,
    DateTime UpdatedAtUtc,
    int? AssignedUserId,
    string? AssignedUserName);

public sealed record McpCardTagSnapshot(
    int Id,
    string Name,
    string StyleName,
    string StylePropertiesJson,
    string? Emoji);

public sealed record BoardGetInput
{
    public int? Id { get; init; }
}

public sealed record BoardListInput;

public sealed record BoardListOutput(
    IReadOnlyList<McpBoardSummary> Boards);

public sealed record CardGetInput
{
    public int? BoardId { get; init; }
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
    public int? CardTypeId { get; init; }
    public int? AssignedUserId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string>? TagNames { get; init; }
}

public sealed record CardUpdateInput
{
    private int? _assignedUserId;
    private bool _assignedUserIdSpecified;

    public int? BoardId { get; init; }
    public int? Id { get; init; }
    public int? ColumnId { get; init; }
    public int? CardTypeId { get; init; }
    public int? AssignedUserId
    {
        get => _assignedUserId;
        init
        {
            _assignedUserId = value;
            _assignedUserIdSpecified = true;
        }
    }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string> TagNames { get; init; } = [];

    [JsonIgnore]
    public bool AssignedUserIdSpecified => _assignedUserIdSpecified;
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
