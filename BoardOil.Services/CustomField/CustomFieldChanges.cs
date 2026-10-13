using System.Text.Json;
using BoardOil.Contracts.CustomField;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Card;

namespace BoardOil.Services.CustomField;

internal static class CustomFieldChanges
{
    public static CustomFieldRuleResult<IReadOnlyList<CustomFieldChange>> Plan(
        IReadOnlyList<EntityBoardCard> cards,
        IReadOnlyList<CustomFieldDefinition> source,
        IReadOnlyList<CustomFieldDefinition> destination,
        bool changingType = false)
    {
        var changes = new List<CustomFieldChange>();
        foreach (var card in cards)
        {
            var originalValues = CustomFieldJsonCodec.DeserialiseValues(card.CustomFieldValuesJson);
            var result = changingType
                ? CustomFieldConverter.ChangeType(source, originalValues, destination)
                : CustomFieldConverter.ApplyDefinitionChanges(source, originalValues, destination);
            if (!result.IsValid)
            {
                return new(null, result.Errors.Select(error =>
                    new ValidationError($"cards[{card.BoardCardId}].{error.Property}", error.Message)).ToArray());
            }

            var converted = result.Value!;
            var valuesChanged = originalValues.Count != converted.Values.Count
                || originalValues.Any(pair => !converted.Values.TryGetValue(pair.Key, out var value)
                    || !JsonElement.DeepEquals(pair.Value, value));
            changes.Add(new(card, CustomFieldJsonCodec.SerialiseValues(converted.Values), converted.LostValues,
                changingType || valuesChanged));
        }
        return new(changes, []);
    }

    public static void Apply(IReadOnlyList<CustomFieldChange> changes, string sourceTypeName,
        string destinationTypeName, string reason = "field definition change")
    {
        var now = DateTime.UtcNow;
        foreach (var change in changes)
        {
            if (change.Changed)
            {
                change.Card.CustomFieldValuesJson = change.ValuesJson;
                change.Card.CardUpdatedUtc = now;
                CardChecklistCounter.Refresh(change.Card);
            }
            CustomFieldLossComment.Add(change.Card, change.LostValues, sourceTypeName, destinationTypeName, reason);
        }
    }
}

internal sealed record CustomFieldChange(EntityBoardCard Card, string ValuesJson, IReadOnlyList<LostCustomFieldValue> LostValues, bool Changed);
