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

public sealed record CreateCardRequest(
    int BoardColumnId,
    string Title,
    string Description,
    IReadOnlyList<string>? TagNames,
    int? CardTypeId = null);

public sealed record UpdateCardRequest(
    string Title,
    string Description,
    IReadOnlyList<string> TagNames,
    int CardTypeId);

public sealed record MoveCardRequest(
    int BoardColumnId,
    int? PositionAfterCardId);
