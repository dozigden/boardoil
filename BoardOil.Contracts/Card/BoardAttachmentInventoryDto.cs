namespace BoardOil.Contracts.Card;

public sealed record BoardAttachmentInventoryDto(
    IReadOnlyList<BoardAttachmentInventoryItemDto> Items,
    int TotalCount,
    long TotalByteLength,
    int MatchingCount,
    int Offset,
    int Limit);

public sealed record BoardAttachmentInventoryItemDto(
    int Id,
    string OriginalFileName,
    string ContentType,
    long ByteLength,
    DateTime CreatedAtUtc,
    int CardId,
    string CardTitle,
    bool Archived);
