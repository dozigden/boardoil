namespace BoardOil.Contracts.Card;

public sealed record CardTagDto(
    int Id,
    string Name,
    string StyleName,
    string StylePropertiesJson,
    string? Emoji);

public sealed record CardCommentDto(
    int Id,
    int CardId,
    int? AuthorUserId,
    string Text,
    DateTime PostedAtUtc,
    DateTime CreatedAtUtc,
    string? AuthorDisplayName = null,
    string? AuthorImageRelativePath = null);

public sealed record CardDto(
    int Id,
    int BoardColumnId,
    int CardTypeId,
    string CardTypeName,
    string? CardTypeEmoji,
    string Title,
    string Description,
    string SortKey,
    IReadOnlyList<CardTagDto> Tags,
    IReadOnlyList<string> TagNames,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int? AssignedUserId = null,
    string? AssignedUserName = null,
    string? AssignedUserImageRelativePath = null,
    int? SlickId = null,
    string? SlickName = null);

public sealed record ArchivedCardDto(
    int Id,
    int BoardId,
    int OriginalCardId,
    string Title,
    IReadOnlyList<string> TagNames,
    DateTime ArchivedAtUtc,
    string SnapshotJson);

public sealed record ArchivedCardDetailDto(
    int Id,
    int BoardId,
    int OriginalCardId,
    string Title,
    IReadOnlyList<string> TagNames,
    DateTime ArchivedAtUtc,
    CardDto Card);

public sealed record ArchivedCardListItemDto(
    int Id,
    int BoardId,
    int OriginalCardId,
    string Title,
    IReadOnlyList<string> TagNames,
    DateTime ArchivedAtUtc);

public sealed record ArchivedCardListDto(
    IReadOnlyList<ArchivedCardListItemDto> Items,
    int Offset,
    int Limit,
    int TotalCount);

public sealed record ArchiveCardsRequest(
    IReadOnlyList<int>? CardIds);

public sealed record ArchiveCardsSummaryDto(
    int BoardId,
    int RequestedCount,
    int ArchivedCount);

public sealed record CreateCardRequest(
    int? BoardColumnId,
    string Title,
    string? Description,
    IReadOnlyList<string>? TagNames,
    int? CardTypeId = null,
    int? AssignedUserId = null,
    string? SlickName = null);

public sealed record UpdateCardRequest(
    string Title,
    string Description,
    IReadOnlyList<string> TagNames,
    int CardTypeId,
    int? BoardColumnId = null,
    int? AssignedUserId = null,
    string? SlickName = null);

public sealed record MoveCardRequest(
    int BoardColumnId,
    int? PositionAfterCardId);

public sealed record BulkMoveCardsRequest(
    int TargetColumnId,
    int? PositionAfterCardId);

public sealed record BulkEditSlickRequest(
    string? Name);

public sealed record BulkEditCardsRequest(
    IReadOnlyList<int>? CardIds,
    BulkMoveCardsRequest? Move,
    IReadOnlyList<string>? AddTagNames = null,
    IReadOnlyList<string>? RemoveTagNames = null,
    BulkEditSlickRequest? Slick = null);

public sealed record BulkDeleteCardsRequest(
    IReadOnlyList<int>? CardIds);

public sealed record BulkDeleteCardsSummaryDto(
    int BoardId,
    int RequestedCount,
    int DeletedCount);

public sealed record CreateCardCommentRequest(
    string Text);
