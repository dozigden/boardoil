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
    DateTime CardCreatedUtc,
    DateTime CardUpdatedUtc,
    int? AssignedUserId = null,
    string? AssignedUserDisplayName = null,
    string? AssignedUserImageRelativePath = null,
    int? SlickId = null,
    string? SlickName = null,
    string? ExternalUrl = null);

public sealed record ArchivedCardDto(
    int Id,
    int BoardId,
    string Title,
    IReadOnlyList<string> TagNames,
    DateTime ArchivedAtUtc,
    string SnapshotJson);

public sealed record ArchivedCardDetailDto(
    int Id,
    int BoardId,
    string Title,
    IReadOnlyList<string> TagNames,
    DateTime ArchivedAtUtc,
    CardDto Card);

public sealed record ArchivedCardListItemDto(
    int Id,
    int BoardId,
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
    string? SlickName = null,
    string? ExternalUrl = null);

public sealed record UpdateCardRequest(
    string Title,
    string Description,
    IReadOnlyList<string> TagNames,
    int CardTypeId,
    int? BoardColumnId = null,
    int? AssignedUserId = null,
    string? SlickName = null,
    string? ExternalUrl = null);

public sealed record SearchCardsRequest(
    IReadOnlyList<CardSearchFilterRequest> Filters);

public sealed record CardSearchFilterRequest(
    string Field,
    string Operator,
    string Value);

public static class CardSearchFields
{
    public const string ExternalUrl = "externalUrl";
}

public static class CardSearchOperators
{
    public const string Exact = "exact";
    public const string Contains = "contains";
}

public static class CardSearchLimits
{
    public const int MinimumFilterCount = 1;
    public const int MaximumFilterCount = 10;
}

public sealed record MoveCardRequest(
    int BoardColumnId,
    int? PositionAfterCardId);

public static class CardTransferPolicies
{
    public const string DestinationDefaults = "destinationDefaults";
    public const string KeepMatching = "keepMatching";
    public const string CopyMissing = "copyMissing";
}

public sealed record TransferCardRequest(
    int DestinationBoardId,
    int DestinationColumnId,
    string TransferPolicy);

public sealed record TransferCardResultDto(
    int BoardId,
    CardDto Card);

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
