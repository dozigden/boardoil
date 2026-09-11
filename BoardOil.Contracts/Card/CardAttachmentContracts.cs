namespace BoardOil.Contracts.Card;

public sealed record CardAttachmentDto(int Id, string OriginalFileName, string ContentType, long ByteLength,
    DateTime CreatedAtUtc, int? CreatedByUserId);

public sealed record CardAttachmentListDto(IReadOnlyList<CardAttachmentDto> Items, long MaxUploadByteLength);
