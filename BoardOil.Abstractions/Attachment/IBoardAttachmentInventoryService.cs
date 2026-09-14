using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;

namespace BoardOil.Abstractions.Attachment;

public interface IBoardAttachmentInventoryService
{
    Task<ApiResult<BoardAttachmentInventoryDto>> ListAsync(
        int boardId, int actorUserId, BoardAttachmentInventoryQuery? query = null,
        CancellationToken cancellationToken = default);
}
