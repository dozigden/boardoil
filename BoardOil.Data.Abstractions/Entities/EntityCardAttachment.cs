namespace BoardOil.Data.Abstractions.Entities;

public sealed class EntityCardAttachment : ISupportCreatedAt
{
    public int Id { get; set; }
    public AttachmentState State { get; set; }
    public int? CardId { get; set; }
    public int? ArchivedCardId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string NormalisedFileName { get; set; } = string.Empty;
    public string? LastError { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long ByteLength { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string? ThumbnailStorageKey { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    public EntityBoardCard? Card { get; set; }
    public EntityArchivedCard? ArchivedCard { get; set; }
    public EntityUser? CreatedByUser { get; set; }
}

public enum AttachmentState
{
    Pending = 0,
    Ready = 1,
    PendingDeletion = 2
}
