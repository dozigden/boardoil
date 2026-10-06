using System.Text.Json;
using BoardOil.Contracts.CardField;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.Card;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.CardField;

namespace BoardOil.Services.Card;

public sealed class UpdateCardPlanner
{
    public CardFieldRuleResult<CardFieldUpdatePlan> PlanFields(
        EntityBoardCard card, EntityCardType target, IReadOnlyDictionary<string, JsonElement>? fields)
    {
        if (card.CardTypeId == target.Id && fields is null)
        {
            return new(new(card.FieldValuesJson, [], CardFieldJsonCodec.DeserialiseValues(card.FieldValuesJson)), []);
        }

        var source = CardFieldJsonCodec.DeserialiseDefinitions(card.CardType.FieldDefinitionsJson);
        var values = CardFieldJsonCodec.DeserialiseValues(card.FieldValuesJson);
        if (fields is not null)
        {
            // Submitted values describe the original type, even when this save changes the type.
            var validation = CardFieldRules.NormaliseValues(source, fields);
            if (!validation.IsValid)
            {
                return new(null, validation.Errors);
            }
            values = validation.Value!;
        }

        var originalValues = values;
        IReadOnlyList<LostCardFieldValue> losses = [];
        if (card.CardTypeId != target.Id)
        {
            var destination = CardFieldJsonCodec.DeserialiseDefinitions(target.FieldDefinitionsJson);
            var conversion = CardFieldConverter.ConvertBetweenTypes(source, values, destination);
            if (!conversion.IsValid)
            {
                return new(null, conversion.Errors);
            }
            values = conversion.Value!.Values;
            losses = conversion.Value.LostValues;
        }

        return new(new(CardFieldJsonCodec.SerialiseValues(values), losses, originalValues), []);
    }

    public CardFieldRuleResult<CardTypeChangePreviewDto> PreviewFields(
        EntityBoardCard card, EntityCardType target, IReadOnlyDictionary<string, JsonElement>? fields)
    {
        var plan = PlanFields(card, target, fields);
        if (!plan.IsValid) { return new(null, plan.Errors); }

        var source = CardFieldJsonCodec.DeserialiseDefinitions(card.CardType.FieldDefinitionsJson);
        var destination = CardFieldJsonCodec.DeserialiseDefinitions(target.FieldDefinitionsJson);
        var values = CardFieldJsonCodec.DeserialiseValues(plan.Value!.ValuesJson);
        var destinationsByName = destination.ToDictionary(field => field.DisplayName, StringComparer.OrdinalIgnoreCase);
        var sourceNames = source.Select(field => field.DisplayName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var lostNames = plan.Value.LostValues.Select(loss => loss.DisplayName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = new List<CardTypeChangeRowDto>();
        foreach (var field in source)
        {
            destinationsByName.TryGetValue(field.DisplayName, out var matched);
            rows.Add(new(ToPreviewValue(field, plan.Value.OriginalValues[field.FieldCode]),
                matched is null ? null : ToPreviewValue(matched, values[matched.FieldCode]),
                lostNames.Contains(field.DisplayName)));
        }
        foreach (var field in destination.Where(field => !sourceNames.Contains(field.DisplayName)))
        {
            rows.Add(new(null, ToPreviewValue(field, values[field.FieldCode]), false));
        }

        var requiresConfirmation = card.CardTypeId != target.Id && rows.Any(row =>
            row.Original is null || row.Destination is null || row.LosesValue
            || row.Original.Type != row.Destination.Type
            || !JsonElement.DeepEquals(row.Original.Value, row.Destination.Value));
        return new(new(card.CardType.Name, target.Name, requiresConfirmation, lostNames.Count > 0, rows), []);
    }

    private static CardTypeChangeValueDto ToPreviewValue(CardFieldDefinition field, JsonElement value)
    {
        var type = field switch
        {
            TextFieldDefinition => "text",
            IntegerFieldDefinition => "integer",
            DateFieldDefinition => "date",
            _ => throw new InvalidOperationException("Unsupported field type.")
        };
        return new(field.DisplayName, field.Emoji, type, value);
    }

    public UpdateCardTypeSelectionResult SelectCardType(EntityCardType? selectedCardType)
    {
        if (selectedCardType is null)
        {
            return new UpdateCardTypeSelectionResult(null, ApiErrors.ValidationFailed([new ValidationError("cardTypeId", "Card type does not exist in board.")]));
        }

        return new UpdateCardTypeSelectionResult(selectedCardType, null);
    }

    public bool TagsChanged(EntityBoardCard existingCard, IReadOnlyList<EntityTag> updatedTags)
    {
        var updatedTagNames = updatedTags
            .Select(x => x.Name)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        var existingTagNames = existingCard.CardTags
            .Select(x => x.Tag.Name)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        return !existingTagNames.SequenceEqual(updatedTagNames, StringComparer.Ordinal);
    }
}

public readonly record struct UpdateCardTypeSelectionResult(EntityCardType? SelectedCardType, ApiError? Error);

public sealed record CardFieldUpdatePlan(string ValuesJson, IReadOnlyList<LostCardFieldValue> LostValues,
    IReadOnlyDictionary<string, JsonElement> OriginalValues);
