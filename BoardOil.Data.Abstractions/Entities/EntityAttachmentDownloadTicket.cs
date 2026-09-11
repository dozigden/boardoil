namespace BoardOil.Data.Abstractions.Entities;

public sealed class EntityAttachmentDownloadTicket : ISupportCreatedAt
{
    public int Id { get; set; }
    public string SecretHash { get; set; } = string.Empty;
    public int ActorUserId { get; set; }
    public int BoardId { get; set; }
    public int AttachmentId { get; set; }
    // Owner snapshots, deliberately not foreign keys: lifecycle changes invalidate tickets.
    public int? CardId { get; set; }
    public int? ArchivedCardId { get; set; }
    public int CardNumber { get; set; }
    public int? PersonalAccessTokenId { get; set; }
    public string? OAuthTokenId { get; set; }
    public string? OAuthAuthorizationId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public EntityCardAttachment Attachment { get; set; } = null!;
}
