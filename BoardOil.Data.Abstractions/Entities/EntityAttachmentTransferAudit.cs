namespace BoardOil.Data.Abstractions.Entities;

public sealed class EntityAttachmentTransferAudit
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int ActorUserId { get; set; }
    public int BoardId { get; set; }
    public int AttachmentId { get; set; }
    public AttachmentTransferOperation Operation { get; set; }
    public AttachmentTransferCredentialType CredentialType { get; set; }
    public AttachmentTransferAuditOutcome Outcome { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

public enum AttachmentTransferOperation
{
    Download = 0,
    Upload = 1
}

public enum AttachmentTransferCredentialType
{
    PersonalAccessToken = 0,
    OAuth = 1
}

public enum AttachmentTransferAuditOutcome
{
    Issued = 0,
    DownloadAdmitted = 1,
    CredentialInvalid = 2,
    OwnerChanged = 3,
    AccessDenied = 4,
    AttachmentUnavailable = 5,
    Expired = 6,
    UploadClaimed = 7,
    UploadCompleted = 8,
    UploadFailed = 9,
    UploadAlreadyClaimed = 10,
    UploadCompletedReturned = 11,
    UploadRequestInvalid = 12,
    UploadRecoveredAtStartup = 13
}
