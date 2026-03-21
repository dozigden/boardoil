using BoardOil.Contracts.Card;
using BoardOil.Persistence.Abstractions.Entities;

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

    public static CardDto ToCardDto(this EntityBoardCard card, int position) =>
        new(
            card.Id,
            card.BoardColumnId,
            card.Title,
            card.Description,
            position,
            card.CardTags
                .Select(x => x.TagName)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList(),
            card.CreatedAtUtc,
            card.UpdatedAtUtc);
}
