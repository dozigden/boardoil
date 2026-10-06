using System.Text.Json.Serialization;
using BoardOil.Contracts.CardField;

namespace BoardOil.Contracts.CardType;

// REST inputs preserve invalid scalar values; services receive validated definitions.
public sealed record CardTypeInput(
    string Name,
    string? Emoji = null,
    string? StyleName = null,
    string? StylePropertiesJson = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<CardFieldDefinitionInput>? Fields = null);

public sealed record PreviewCardTypeFieldsInput(IReadOnlyList<CardFieldDefinitionInput>? Fields);
