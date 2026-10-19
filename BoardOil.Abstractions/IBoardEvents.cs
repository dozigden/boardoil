using BoardOil.Contracts.Card;
using BoardOil.Contracts.Configuration;
using BoardOil.Contracts.Column;

namespace BoardOil.Abstractions;

public interface IBoardEvents
{
    Task ColumnCreatedAsync(int boardId, ColumnDto column);
    Task ColumnUpdatedAsync(int boardId, ColumnDto column);
    Task ColumnsReorderedAsync(int boardId, IReadOnlyList<ColumnDto> columns);
    Task ColumnDeletedAsync(int boardId, int columnId);

    Task CardCreatedAsync(int boardId, CardDto card);
    Task CardUpdatedAsync(int boardId, CardDto card);
    Task CardDeletedAsync(int boardId, int cardId);
    Task CardMovedAsync(int boardId, CardDto card);
    Task CommentCreatedAsync(int boardId, CardCommentDto comment);
    Task CommentUpdatedAsync(int boardId, CardCommentDto comment);
    Task CommentDeletedAsync(int boardId, int cardId, int commentId);
    Task AttachmentAddedAsync(int boardId, int cardId, CardAttachmentDto attachment);
    Task AttachmentDeletedAsync(int boardId, int cardId, int attachmentId);

    Task TagsChangedAsync(int boardId);
    Task ResyncRequestedAsync(int boardId);
    Task SystemInfoMessageUpdatedAsync(SystemInfoMessageDto? systemInfoMessage);
}
