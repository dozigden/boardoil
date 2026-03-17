using BoardOil.Ef.Entities;

namespace BoardOil.Services.Card;

public static class CardMappingExtensions
{
    public static CardDto ToCardDto(this BoardCard card, int position) =>
        new(
            card.Id,
            card.BoardColumnId,
            card.Title,
            card.Description,
            position,
            card.CreatedAtUtc,
            card.UpdatedAtUtc);
}
