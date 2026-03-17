namespace BoardOil.Contracts.Board;

public sealed record BoardRecord(
    int Id,
    string Name,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record BoardCreateRecord(
    string Name,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
