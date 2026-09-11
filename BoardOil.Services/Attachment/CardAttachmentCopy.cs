namespace BoardOil.Services.Attachment;

public sealed record CardAttachmentCopy(
    int SourceId,
    int BoardId,
    int CardNumber,
    IReadOnlyList<int> OriginalAttachmentIds,
    IReadOnlyList<PreparedAttachmentFile> Files);
