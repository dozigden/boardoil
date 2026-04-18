using BoardOil.Contracts.Card;

namespace BoardOil.Contracts.Board;

public sealed record BoardSummaryDto(
    int Id,
    string Name,
    string Description,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? CurrentUserRole);

public sealed record SystemBoardSummaryDto(
    int Id,
    string Name,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record BoardDto(
    int Id,
    string Name,
    string Description,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? CurrentUserRole,
    IReadOnlyList<BoardColumnDto> Columns);

public sealed record CreateBoardRequest(
    string Name,
    string? Description = null);

public sealed record ImportTasksMdBoardRequest(
    string Url);

public sealed record ImportBoardPackageRequest(
    string? Name,
    byte[] PackageContent);

public sealed record UpdateBoardRequest(
    string Name,
    string? Description = null);

public sealed record BoardColumnDto(
    int Id,
    string Title,
    string SortKey,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<CardDto> Cards);
