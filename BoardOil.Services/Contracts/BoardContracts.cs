namespace BoardOil.Services.Contracts;

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
