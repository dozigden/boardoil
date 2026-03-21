using BoardOil.Contracts.Card;

namespace BoardOil.Services.Card;

public static class CardMappingExtensions
{
    public static CardDto ToCardDto(this CardRecord card, int position) =>
        new(
            card.Id,
            card.BoardColumnId,
            card.Title,
            card.Description,
            position,
            card.TagNames,
            card.CreatedAtUtc,
            card.UpdatedAtUtc);
}
