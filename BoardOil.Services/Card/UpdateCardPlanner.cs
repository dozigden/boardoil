using BoardOil.Contracts.Common;
using BoardOil.Contracts.Card;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.CardField;

namespace BoardOil.Services.Card;

public sealed class UpdateCardPlanner
{
    public CardFieldRuleResult<CardFieldUpdatePlan> PlanFields(
        EntityBoardCard card, EntityCardType target, UpdateCardRequest request)
    {
        if (card.CardTypeId == target.Id && request.Fields is null)
        {
            return new(new(card.FieldValuesJson, []), []);
        }

        var source = CardFieldJsonCodec.DeserialiseDefinitions(card.CardType.FieldDefinitionsJson);
        var values = CardFieldJsonCodec.DeserialiseValues(card.FieldValuesJson);
        if (request.Fields is not null)
        {
            // Submitted values describe the original type, even when this save changes the type.
            var validation = CardFieldRules.NormaliseValues(source, request.Fields);
            if (!validation.IsValid)
            {
                return new(null, validation.Errors);
            }
            values = validation.Value!;
        }

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

        return new(new(CardFieldJsonCodec.SerialiseValues(values), losses), []);
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

public sealed record CardFieldUpdatePlan(string ValuesJson, IReadOnlyList<LostCardFieldValue> LostValues);
