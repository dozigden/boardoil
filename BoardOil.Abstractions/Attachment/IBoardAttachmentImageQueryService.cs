using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;

namespace BoardOil.Abstractions.Attachment;

public interface IBoardAttachmentImageQueryService
{
    Task<ApiResult<IReadOnlyList<CardAttachmentImageCandidateDto>>> ListFirstByCardAsync(
        int boardId,
        IReadOnlyList<int>? cardIds,
        int actorUserId,
        CancellationToken cancellationToken = default);
}
