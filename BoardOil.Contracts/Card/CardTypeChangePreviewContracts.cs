using System.Text.Json;

namespace BoardOil.Contracts.Card;

public sealed record PreviewCardTypeChangeRequest(
    int CardTypeId,
    IReadOnlyDictionary<string, JsonElement>? Fields = null);

public sealed record CardTypeChangePreviewDto(
    string OriginalTypeName,
    string DestinationTypeName,
    bool RequiresConfirmation,
    bool HasLosses,
    IReadOnlyList<CardTypeChangeRowDto> Rows);

public sealed record CardTypeChangeRowDto(
    CardTypeChangeValueDto? Original,
    CardTypeChangeValueDto? Destination,
    bool LosesValue);

public sealed record CardTypeChangeValueDto(
    string DisplayName,
    string? Emoji,
    string Type,
    JsonElement Value);
