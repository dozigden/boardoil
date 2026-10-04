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

        var definitions = CardFieldJsonCodec.DeserialiseDefinitions(target.FieldDefinitionsJson);
        var values = CardFieldJsonCodec.DeserialiseValues(card.FieldValuesJson);
        IReadOnlyList<LostCardFieldValue> losses = [];
        if (card.CardTypeId != target.Id)
        {
            var conversion = CardFieldConverter.ConvertBetweenTypes(
                CardFieldJsonCodec.DeserialiseDefinitions(card.CardType.FieldDefinitionsJson), values, definitions);
            if (!conversion.IsValid)
            {
                return new(null, conversion.Errors);
            }
            values = conversion.Value!.Values;
            losses = conversion.Value.LostValues;
        }

        if (request.Fields is not null)
        {
            var validation = CardFieldRules.NormaliseValues(definitions, request.Fields);
            if (!validation.IsValid)
            {
                return new(null, validation.Errors);
            }
            values = validation.Value!;
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
