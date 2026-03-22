using BoardOil.Contracts.Card;
using BoardOil.Persistence.Abstractions.Entities;

namespace BoardOil.Services.Card;

public static class CardMappingExtensions
{
    public static CardDto ToCardDto(this CardRecord card) =>
        new(
            card.Id,
            card.BoardColumnId,
            card.Title,
            card.Description,
            card.SortKey,
            card.TagNames,
            card.CreatedAtUtc,
            card.UpdatedAtUtc);

    public static CardDto ToCardDto(this EntityBoardCard card) =>
        new(
            card.Id,
            card.BoardColumnId,
            card.Title,
            card.Description,
            card.SortKey,
            card.CardTags
                .Select(x => x.TagName)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList(),
            card.CreatedAtUtc,
            card.UpdatedAtUtc);
}
