using System.Text.Json.Serialization;
using BoardOil.Contracts.CardField;

namespace BoardOil.Contracts.CardType;

public sealed record CardTypeDto(
    int Id,
    string Name,
    string? Emoji,
    string StyleName,
    string StylePropertiesJson,
    bool IsSystem,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public IReadOnlyList<CardFieldDefinition> Fields { get; init; } = [];
}

public sealed record CreateCardTypeRequest(
    string Name,
    string? Emoji = null,
    string? StyleName = null,
    string? StylePropertiesJson = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<CardFieldDefinition>? Fields = null);

public sealed record UpdateCardTypeRequest(
    string Name,
    string? Emoji = null,
    string? StyleName = null,
    string? StylePropertiesJson = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<CardFieldDefinition>? Fields = null);
