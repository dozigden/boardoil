namespace BoardOil.Contracts.Card;

public sealed record CardDto(
    int Id,
    int BoardColumnId,
    string Title,
    string Description,
    int Position,
    IReadOnlyList<string> TagNames,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CreateCardRequest(
    int BoardColumnId,
    string Title,
    string Description,
    int? Position,
    IReadOnlyList<string>? TagNames);

public sealed record UpdateCardRequest(
    int? BoardColumnId,
    string? Title,
    string? Description,
    int? Position,
    IReadOnlyList<string>? TagNames);

public sealed record CardRecord(
    int Id,
    int BoardColumnId,
    string Title,
    string Description,
    string SortKey,
    IReadOnlyList<string> TagNames,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CreateCardRecord(
    int BoardColumnId,
    string Title,
    string Description,
    string SortKey,
    IReadOnlyList<string> TagNames,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record UpdateCardRecord(
    int Id,
    int BoardColumnId,
    string Title,
    string Description,
    string SortKey,
    IReadOnlyList<string>? TagNames,
    DateTime UpdatedAtUtc);
