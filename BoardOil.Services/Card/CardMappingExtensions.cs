using BoardOil.Contracts.Card;
using BoardOil.Persistence.Abstractions.Entities;
using System.Text.Json;

namespace BoardOil.Services.Card;

public static class CardMappingExtensions
{
    public static CardDto ToCardDto(this EntityBoardCard card) =>
        new(
            card.Id,
            card.BoardColumnId,
            card.CardTypeId,
            card.CardType.Name,
            card.CardType.Emoji,
            card.Title,
            card.Description,
            card.SortKey,
            card.CardTags
                .Select(x => x.Tag.ToCardTagDto())
                .OrderBy(x => x.Name, StringComparer.Ordinal)
                .ToList(),
            card.CardTags
                .Select(x => x.Tag.Name)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList(),
            card.CreatedAtUtc,
            card.UpdatedAtUtc);

    public static ArchivedCardDto ToArchivedCardDto(this EntityArchivedCard archivedCard) =>
        new(
            archivedCard.Id,
            archivedCard.BoardId,
            archivedCard.OriginalCardId,
            archivedCard.SearchTitle,
            ParseSearchTagsJson(archivedCard.SearchTagsJson),
            archivedCard.ArchivedAtUtc,
            archivedCard.SnapshotJson);

    private static CardTagDto ToCardTagDto(this EntityTag tag) =>
        new(
            tag.Id,
            tag.Name,
            tag.StyleName,
            tag.StylePropertiesJson,
            tag.Emoji);

    private static IReadOnlyList<string> ParseSearchTagsJson(string searchTagsJson)
    {
        if (string.IsNullOrWhiteSpace(searchTagsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<string>>(searchTagsJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
