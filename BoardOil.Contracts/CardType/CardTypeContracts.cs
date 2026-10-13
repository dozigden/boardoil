using System.Text.Json.Serialization;
using BoardOil.Contracts.CustomField;

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
    public IReadOnlyList<CustomFieldDefinition> CustomFields { get; init; } = [];
}

public sealed record CreateCardTypeRequest(
    string Name,
    string? Emoji = null,
    string? StyleName = null,
    string? StylePropertiesJson = null,
    int? CopyFieldAssignmentsFromCardTypeId = null);

public sealed record UpdateCardTypeRequest(
    string Name,
    string? Emoji = null,
    string? StyleName = null,
    string? StylePropertiesJson = null);
