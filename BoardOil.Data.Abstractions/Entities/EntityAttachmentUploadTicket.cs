namespace BoardOil.Data.Abstractions.Entities;

public sealed class EntityAttachmentUploadTicket : ISupportCreatedAt
{
    public int Id { get; set; }
    public string SecretHash { get; set; } = string.Empty;
    public int ActorUserId { get; set; }
    public int BoardId { get; set; }
    public int CardId { get; set; }
    public int CardNumber { get; set; }
    public int AttachmentId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long DeclaredByteLength { get; set; }
    public int? PersonalAccessTokenId { get; set; }
    public string? OAuthTokenId { get; set; }
    public string? OAuthAuthorizationId { get; set; }
    public AttachmentUploadTicketState State { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? FailedAtUtc { get; set; }
    public EntityCardAttachment Attachment { get; set; } = null!;
}

public enum AttachmentUploadTicketState
{
    Issued = 0,
    Uploading = 1,
    Completed = 2,
    Failed = 3
}
