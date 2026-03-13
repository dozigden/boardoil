using BoardOil.Ef.Entities;
using BoardOil.Services.Contracts;

namespace BoardOil.Services.Mappings;

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
