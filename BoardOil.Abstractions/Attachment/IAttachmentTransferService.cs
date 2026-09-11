using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;

namespace BoardOil.Abstractions.Attachment;

public interface IAttachmentTransferService
{
    Task<ApiResult<AttachmentDownloadTicket>> IssueDownloadAsync(int boardId, int attachmentId, int actorUserId,
        AttachmentTransferCredential credential, CancellationToken cancellationToken = default);
    Task<ApiResult<AttachmentDownload>> DownloadAsync(int ticketId, string secret, CancellationToken cancellationToken = default);
    Task<ApiResult<AttachmentUploadTicket>> IssueUploadAsync(int boardId, int cardId, int actorUserId,
        string fileName, string? contentType, long byteLength, AttachmentTransferCredential credential,
        CancellationToken cancellationToken = default);
    Task<ApiResult<CardAttachmentDto>> UploadAsync(int ticketId, string secret, string? contentType,
        long? contentLength, Stream content, CancellationToken cancellationToken = default);
}

// References only: never retain the originating bearer token.
public sealed record AttachmentTransferCredential(int? PersonalAccessTokenId, string? OAuthTokenId, string? OAuthAuthorizationId);

public sealed record AttachmentDownloadTicket(int Id, string Secret, DateTime ExpiresAtUtc)
{
    public override string ToString() => $"Attachment download ticket {Id}, expires {ExpiresAtUtc:O}";
}

public sealed record AttachmentUploadTicket(int Id, string Secret, string ContentType, long ByteLength, DateTime ExpiresAtUtc)
{
    public override string ToString() => $"Attachment upload ticket {Id}, expires {ExpiresAtUtc:O}";
}

// OAuth token validation belongs to the API's authentication infrastructure.
public interface IAttachmentTransferCredentialValidator
{
    Task<ApiResult<DateTime?>> ValidateAsync(int actorUserId, AttachmentTransferCredential credential, string requiredScope,
        CancellationToken cancellationToken = default);
}

public static class AttachmentTransferAuditRetention
{
    public const int Days = 14;
}
