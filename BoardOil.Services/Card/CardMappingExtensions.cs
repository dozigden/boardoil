using BoardOil.Contracts.Card;
using BoardOil.Data.Abstractions.Entities;
using System.Text.Json;

namespace BoardOil.Services.Card;

public static class CardMappingExtensions
{
    private const string UnknownCommentAuthorDisplayName = "Unknown user";

    public static CardDto ToCardDto(this EntityBoardCard card) =>
        new(
            card.RequireBoardCardId(),
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
            card.UpdatedAtUtc,
            card.AssignedUserId,
            card.AssignedUser?.DisplayName,
            null,
            card.SlickId,
            card.Slick?.Name,
            card.ExternalUrl);

    public static CardDto WithAssignedUserImageRelativePath(this CardDto card, string? assignedUserImageRelativePath) =>
        card with
        {
            AssignedUserImageRelativePath = assignedUserImageRelativePath
        };

    public static ArchivedCardDto ToArchivedCardDto(this EntityArchivedCard archivedCard) =>
        new(
            archivedCard.OriginalCardId,
            archivedCard.BoardId,
            archivedCard.SearchTitle,
            ParseSearchTagsJson(archivedCard.SearchTagsJson),
            archivedCard.ArchivedAtUtc,
            archivedCard.SnapshotJson);

    public static ArchivedCardDetailDto ToArchivedCardDetailDto(this EntityArchivedCard archivedCard, CardDto card) =>
        new(
            archivedCard.OriginalCardId,
            archivedCard.BoardId,
            archivedCard.SearchTitle,
            ParseSearchTagsJson(archivedCard.SearchTagsJson),
            archivedCard.ArchivedAtUtc,
            card);

    public static ArchivedCardListItemDto ToArchivedCardListItemDto(this EntityArchivedCard archivedCard) =>
        new(
            archivedCard.OriginalCardId,
            archivedCard.BoardId,
            archivedCard.SearchTitle,
            ParseSearchTagsJson(archivedCard.SearchTagsJson),
            archivedCard.ArchivedAtUtc);

    private static CardTagDto ToCardTagDto(this EntityTag tag) =>
        new(
            tag.Id,
            tag.Name,
            tag.StyleName,
            tag.StylePropertiesJson,
            tag.Emoji);

    public static int RequireBoardCardId(this EntityBoardCard card) =>
        card.BoardCardId
            ?? throw new InvalidOperationException($"Card '{card.Id}' does not have a board-scoped ID.");

    public static CardCommentDto ToCardCommentDto(
        this EntityCardComment comment,
        int boardCardId,
        string? authorDisplayName = null,
        string? authorImageRelativePath = null) =>
        new(
            comment.Id,
            boardCardId,
            comment.AuthorUserId,
            comment.Text,
            comment.PostedAtUtc,
            comment.CreatedAtUtc,
            authorDisplayName ?? comment.AuthorUser?.DisplayName ?? UnknownCommentAuthorDisplayName,
            authorImageRelativePath);

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
