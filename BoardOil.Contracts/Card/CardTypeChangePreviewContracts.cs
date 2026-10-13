using System.Text.Json;

namespace BoardOil.Contracts.Card;

public sealed record PreviewCardTypeChangeRequest(
    int CardTypeId,
    IReadOnlyDictionary<string, JsonElement>? CustomFieldValues = null);

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
    string Name,
    string? Emoji,
    string DataType,
    JsonElement Value);
