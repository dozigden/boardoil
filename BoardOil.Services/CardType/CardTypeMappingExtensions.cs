using BoardOil.Contracts.CardType;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.CustomField;

namespace BoardOil.Services.CardType;

public static class CardTypeMappingExtensions
{
    public static CardTypeDto ToCardTypeDto(this EntityCardType cardType) =>
        new(
            cardType.Id,
            cardType.Name,
            cardType.Emoji,
            cardType.StyleName,
            cardType.StylePropertiesJson,
            cardType.IsSystem,
            cardType.CreatedAtUtc,
            cardType.UpdatedAtUtc)
        {
            CustomFields = cardType.Board.CustomFields.GetApplicableCustomFieldDefinitions(cardType.Id)
        };
}
