using BoardOil.Contracts.CardType;
using BoardOil.Data.Abstractions.Entities;

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
            cardType.UpdatedAtUtc);
}
