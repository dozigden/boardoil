namespace BoardOil.Contracts.Card;

public sealed record CardTagDto(
    int Id,
    string Name,
    string StyleName,
    string StylePropertiesJson,
    string? Emoji);

public sealed record CardDto(
    int Id,
    int BoardColumnId,
    int CardTypeId,
    string CardTypeName,
    string? CardTypeEmoji,
    string Title,
    string Description,
    string SortKey,
    IReadOnlyList<CardTagDto> Tags,
    IReadOnlyList<string> TagNames,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record ArchivedCardDto(
    int Id,
    int BoardId,
    int OriginalCardId,
    string Title,
    IReadOnlyList<string> TagNames,
    DateTime ArchivedAtUtc,
    string SnapshotJson);

public sealed record ArchivedCardListItemDto(
    int Id,
    int BoardId,
    int OriginalCardId,
    string Title,
    IReadOnlyList<string> TagNames,
    DateTime ArchivedAtUtc);

public sealed record ArchivedCardListDto(
    IReadOnlyList<ArchivedCardListItemDto> Items,
    int Offset,
    int Limit,
    int TotalCount);

public sealed record CreateCardRequest(
    int? BoardColumnId,
    string Title,
    string? Description,
    IReadOnlyList<string>? TagNames,
    int? CardTypeId = null);

public sealed record UpdateCardRequest(
    string Title,
    string Description,
    IReadOnlyList<string> TagNames,
    int CardTypeId,
    int? BoardColumnId = null);

public sealed record MoveCardRequest(
    int BoardColumnId,
    int? PositionAfterCardId);
