using System.Text.Json;
using BoardOil.Contracts.CardField;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Card;
using BoardOil.Services.CardField;

namespace BoardOil.Services.CardType;

internal static class CardTypeFieldChanges
{
    public static CardFieldRuleResult<IReadOnlyList<CardFieldChange>> Plan(
        IReadOnlyList<EntityBoardCard> cards,
        IReadOnlyList<CardFieldDefinition> source,
        IReadOnlyList<CardFieldDefinition> destination,
        bool changingType = false)
    {
        var changes = new List<CardFieldChange>();
        foreach (var card in cards)
        {
            var originalValues = CardFieldJsonCodec.DeserialiseValues(card.FieldValuesJson);
            var result = changingType
                ? CardFieldConverter.ConvertBetweenTypes(source, originalValues, destination)
                : CardFieldConverter.ApplyDefinitionChanges(source, originalValues, destination);
            if (!result.IsValid)
            {
                return new(null, result.Errors.Select(error =>
                    new ValidationError($"cards[{card.BoardCardId}].{error.Property}", error.Message)).ToArray());
            }

            var converted = result.Value!;
            var valuesChanged = originalValues.Count != converted.Values.Count
                || originalValues.Any(pair => !converted.Values.TryGetValue(pair.Key, out var value)
                    || !JsonElement.DeepEquals(pair.Value, value));
            changes.Add(new(card, CardFieldJsonCodec.SerialiseValues(converted.Values), converted.LostValues,
                changingType || valuesChanged));
        }
        return new(changes, []);
    }

    public static void Apply(IReadOnlyList<CardFieldChange> changes, string reason = "field definition change")
    {
        var now = DateTime.UtcNow;
        foreach (var change in changes)
        {
            change.Card.FieldValuesJson = change.ValuesJson;
            if (change.Changed)
            {
                change.Card.CardUpdatedUtc = now;
                CardChecklistCounter.Refresh(change.Card);
            }
            CardFieldLossComment.Add(change.Card, change.LostValues, reason);
        }
    }
}

internal sealed record CardFieldChange(EntityBoardCard Card, string ValuesJson, IReadOnlyList<LostCardFieldValue> LostValues, bool Changed);
