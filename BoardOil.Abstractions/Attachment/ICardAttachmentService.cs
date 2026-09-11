using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;

namespace BoardOil.Abstractions.Attachment;

public interface ICardAttachmentService
{
    Task<ApiResult<CardAttachmentListDto>> ListAsync(int boardId, int cardId, bool archived, int actorUserId);
    Task<ApiResult<int>> CheckUploadAccessAsync(int boardId, int cardId, int actorUserId);
    Task<ApiResult<CardAttachmentDto>> UploadAsync(int boardId, int cardId, int actorUserId,
        string fileName, string? contentType, Stream content, CancellationToken cancellationToken = default);
    Task<ApiResult<AttachmentDownload>> DownloadAsync(int boardId, int attachmentId, int actorUserId);
    Task<ApiResult> DeleteAsync(int boardId, int cardId, int attachmentId, int actorUserId);
}

public sealed record AttachmentDownload(string FileName, Stream Content);
