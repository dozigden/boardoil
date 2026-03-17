namespace BoardOil.Contracts.Card;

public sealed record CardDto(
    int Id,
    int BoardColumnId,
    string Title,
    string Description,
    int Position,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CreateCardRequest(
    int BoardColumnId,
    string Title,
    string Description,
    int? Position);

public sealed record UpdateCardRequest(
    int? BoardColumnId,
    string? Title,
    string? Description,
    int? Position);

public sealed record CardRecord(
    int Id,
    int BoardColumnId,
    string Title,
    string Description,
    string SortKey,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CreateCardRecord(
    int BoardColumnId,
    string Title,
    string Description,
    string SortKey,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record UpdateCardRecord(
    int Id,
    int BoardColumnId,
    string Title,
    string Description,
    string SortKey,
    DateTime UpdatedAtUtc);
