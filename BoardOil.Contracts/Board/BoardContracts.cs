using BoardOil.Contracts.Card;

namespace BoardOil.Contracts.Board;

public sealed record BoardDto(
    int Id,
    string Name,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<BoardColumnDto> Columns);

public sealed record BoardColumnDto(
    int Id,
    string Title,
    int Position,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<CardDto> Cards);
