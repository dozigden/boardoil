namespace BoardOil.Services.Contracts;

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
