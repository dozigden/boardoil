using System.Text.Json;
using BoardOil.Contracts.CustomField;
using BoardOil.Contracts.Common;
using BoardOil.Contracts.Card;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.CustomField;

namespace BoardOil.Services.Card;

public sealed class UpdateCardPlanner
{
    public CustomFieldValidationResult<CustomFieldUpdatePlan> PlanFields(
        EntityBoardCard card, EntityCardType target, IReadOnlyDictionary<string, JsonElement>? fields)
    {
        if (card.CardTypeId == target.Id && fields is null)
        {
            return new(new(card.CustomFieldValuesJson, [], CustomFieldJsonCodec.DeserialiseValues(card.CustomFieldValuesJson)), []);
        }

        var source = card.CardType.Board.CustomFields.GetApplicableCustomFieldDefinitions(card.CardType.Id);
        var values = CustomFieldJsonCodec.DeserialiseValues(card.CustomFieldValuesJson);
        if (fields is not null)
        {
            // Submitted values describe the original type, even when this save changes the type.
            var validation = CustomFieldValidator.ValidateAndNormaliseValues(source, fields);
            if (!validation.IsValid)
            {
                return new(null, validation.Errors);
            }
            values = validation.Value!;
        }

        var originalValues = values;
        IReadOnlyList<LostCustomFieldValue> losses = [];
        if (card.CardTypeId != target.Id)
        {
            var destination = target.Board.CustomFields.GetApplicableCustomFieldDefinitions(target.Id);
            var conversion = CustomFieldConverter.ConvertWithinBoard(source, values, destination);
            if (!conversion.IsValid)
            {
                return new(null, conversion.Errors);
            }
            values = conversion.Value!.Values;
            losses = conversion.Value.LostValues;
        }

        return new(new(CustomFieldJsonCodec.SerialiseValues(values), losses, originalValues), []);
    }

    public CustomFieldValidationResult<CardTypeChangePreviewDto> PreviewFields(
        EntityBoardCard card, EntityCardType target, IReadOnlyDictionary<string, JsonElement>? fields)
    {
        var plan = PlanFields(card, target, fields);
        if (!plan.IsValid) { return new(null, plan.Errors); }

        var source = card.CardType.Board.CustomFields.GetApplicableCustomFieldDefinitions(card.CardType.Id);
        var destination = target.Board.CustomFields.GetApplicableCustomFieldDefinitions(target.Id);
        var values = CustomFieldJsonCodec.DeserialiseValues(plan.Value!.ValuesJson);
        var destinationsByCode = destination.ToDictionary(field => field.Code, StringComparer.OrdinalIgnoreCase);
        var sourceCodes = source.Select(field => field.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var lostNames = plan.Value.LostValues.Select(loss => loss.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = new List<CardTypeChangeRowDto>();
        foreach (var field in source)
        {
            destinationsByCode.TryGetValue(field.Code, out var matched);
            rows.Add(new(ToPreviewValue(field, plan.Value.OriginalValues[field.Code]),
                matched is null ? null : ToPreviewValue(matched, values[matched.Code]),
                lostNames.Contains(field.Name)));
        }
        foreach (var field in destination.Where(field => !sourceCodes.Contains(field.Code)))
        {
            rows.Add(new(null, ToPreviewValue(field, values[field.Code]), false));
        }

        var requiresConfirmation = card.CardTypeId != target.Id && rows.Any(row =>
            row.Original is null || row.Destination is null || row.LosesValue
            || row.Original.DataType != row.Destination.DataType
            || !JsonElement.DeepEquals(row.Original.Value, row.Destination.Value));
        return new(new(card.CardType.Name, target.Name, requiresConfirmation, lostNames.Count > 0, rows), []);
    }

    private static CardTypeChangeValueDto ToPreviewValue(CustomFieldDefinition field, JsonElement value)
    {
        var type = field switch
        {
            TextCustomFieldDefinition => "text",
            IntegerCustomFieldDefinition => "integer",
            DateCustomFieldDefinition => "date",
            _ => throw new InvalidOperationException("Unsupported field type.")
        };
        return new(field.Name, field.Emoji, type, value);
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

public sealed record CustomFieldUpdatePlan(string ValuesJson, IReadOnlyList<LostCustomFieldValue> LostValues,
    IReadOnlyDictionary<string, JsonElement> OriginalValues);
