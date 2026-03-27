using BoardOil.Contracts.Card;

namespace BoardOil.Contracts.Board;

public sealed record BoardSummaryDto(
    int Id,
    string Name,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record BoardDto(
    int Id,
    string Name,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<BoardColumnDto> Columns);

public sealed record CreateBoardRequest(
    string Name);

public sealed record BoardColumnDto(
    int Id,
    string Title,
    string SortKey,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<CardDto> Cards);
