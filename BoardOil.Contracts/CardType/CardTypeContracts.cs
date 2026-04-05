namespace BoardOil.Contracts.CardType;

public sealed record CardTypeDto(
    int Id,
    string Name,
    string? Emoji,
    bool IsSystem,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CreateCardTypeRequest(
    string Name,
    string? Emoji = null);

public sealed record UpdateCardTypeRequest(
    string Name,
    string? Emoji = null);
