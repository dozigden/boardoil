namespace BoardOil.Contracts.Card;

public sealed record CardAttachmentDto(int Id, string OriginalFileName, string ContentType, long ByteLength,
    DateTime CreatedAtUtc, int? CreatedByUserId, bool HasThumbnail = false);

public sealed record CardAttachmentListDto(IReadOnlyList<CardAttachmentDto> Items, long MaxUploadByteLength);

public sealed record CardAttachmentImageCandidateDto(
    int CardId,
    int AttachmentId,
    string OriginalFileName,
    bool HasThumbnail);
