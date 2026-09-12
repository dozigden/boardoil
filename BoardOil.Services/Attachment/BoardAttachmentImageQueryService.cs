using BoardOil.Abstractions.Attachment;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Attachment;
using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Services.Attachment;

public sealed class BoardAttachmentImageQueryService(
    IAttachmentRepository attachments,
    IBoardAuthorisationService authorisation,
    IDbContextScopeFactory scopes) : IBoardAttachmentImageQueryService
{
    public async Task<ApiResult<IReadOnlyList<CardAttachmentImageCandidateDto>>> ListFirstByCardAsync(
        int boardId,
        IReadOnlyList<int>? cardIds,
        int actorUserId,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopes.CreateReadOnly();
        if (!await authorisation.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardAccess))
        {
            return ApiErrors.Forbidden("You do not have access to this board.");
        }

        var requestedCardIds = cardIds?
            .Where(x => x > 0)
            .Distinct()
            .ToList();
        var query = attachments.Query().Where(x =>
            x.State == AttachmentState.Ready
            && x.Card != null
            && x.Card.BoardId == boardId);
        if (requestedCardIds is { Count: > 0 })
        {
            query = query.Where(x => requestedCardIds.Contains(x.Card!.BoardCardId));
        }

        var attachmentMetadata = await query
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                CardId = x.Card!.BoardCardId,
                AttachmentId = x.Id,
                x.OriginalFileName,
                HasThumbnail = x.ThumbnailStorageKey != null,
            })
            .ToListAsync(cancellationToken);

        var candidates = attachmentMetadata
            .Where(x => AttachmentImageFileName.IsSupported(x.OriginalFileName))
            .GroupBy(x => x.CardId)
            .Select(x => x.First())
            .OrderBy(x => x.CardId)
            .Select(x => new CardAttachmentImageCandidateDto(
                x.CardId,
                x.AttachmentId,
                x.OriginalFileName,
                x.HasThumbnail))
            .ToList();
        return ApiResults.Ok<IReadOnlyList<CardAttachmentImageCandidateDto>>(candidates);
    }
}
