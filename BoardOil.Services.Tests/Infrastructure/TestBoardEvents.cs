using BoardOil.Abstractions;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Configuration;
using BoardOil.Contracts.Column;

namespace BoardOil.Services.Tests.Infrastructure;

public sealed class TestBoardEvents : IBoardEvents
{
    public readonly List<(int BoardId, ColumnDto Column)> ColumnCreatedEvents = [];
    public readonly List<(int BoardId, ColumnDto Column)> ColumnUpdatedEvents = [];
    public readonly List<string> ColumnEventNames = [];
    public Func<Task>? ColumnEventObserver { get; set; }
    public readonly List<(int BoardId, CardCommentDto Comment)> CommentCreatedEvents = [];
    public readonly List<(int BoardId, CardCommentDto Comment)> CommentUpdatedEvents = [];
    public readonly List<(int BoardId, int CardId, int CommentId)> CommentDeletedEvents = [];
    public readonly List<(int BoardId, int ColumnId)> ColumnDeletedEvents = [];
    public readonly List<(int BoardId, IReadOnlyList<ColumnDto> Columns)> ColumnsReorderedEvents = [];
    public readonly List<(int BoardId, CardDto Card)> CardCreatedEvents = [];
    public readonly List<(int BoardId, CardDto Card)> CardUpdatedEvents = [];
    public readonly List<(int BoardId, CardDto Card)> CardMovedEvents = [];
    public readonly List<(int BoardId, int CardId)> CardDeletedEvents = [];
    public readonly List<(int BoardId, int CardId, CardAttachmentDto Attachment)> AttachmentAddedEvents = [];
    public readonly List<(int BoardId, int CardId, int AttachmentId)> AttachmentDeletedEvents = [];
    public readonly List<int> ResyncRequestedBoardIds = [];
    public readonly List<int> TagsChangedBoardIds = [];
    public readonly List<string> PublishedEventNames = [];

    public Task ColumnCreatedAsync(int boardId, ColumnDto column)
    {
        ColumnCreatedEvents.Add((boardId, column));
        return RecordColumnEvent("created");
    }
    public Task ColumnUpdatedAsync(int boardId, ColumnDto column)
    {
        ColumnUpdatedEvents.Add((boardId, column));
        return RecordColumnEvent("updated");
    }
    public Task ColumnsReorderedAsync(int boardId, IReadOnlyList<ColumnDto> columns)
    {
        ColumnsReorderedEvents.Add((boardId, columns));
        return RecordColumnEvent("reordered");
    }
    public Task ColumnDeletedAsync(int boardId, int columnId)
    {
        ColumnDeletedEvents.Add((boardId, columnId));
        return RecordColumnEvent("deleted");
    }

    private Task RecordColumnEvent(string name)
    {
        ColumnEventNames.Add(name);
        return ColumnEventObserver?.Invoke() ?? Task.CompletedTask;
    }

    public Task CardCreatedAsync(int boardId, CardDto card)
    {
        CardCreatedEvents.Add((boardId, card));
        PublishedEventNames.Add("card-created");
        return Task.CompletedTask;
    }
    public Task CardUpdatedAsync(int boardId, CardDto card)
    {
        CardUpdatedEvents.Add((boardId, card));
        return Task.CompletedTask;
    }
    public Task CardDeletedAsync(int boardId, int cardId)
    {
        CardDeletedEvents.Add((boardId, cardId));
        PublishedEventNames.Add("card-deleted");
        return Task.CompletedTask;
    }
    public Task CardMovedAsync(int boardId, CardDto card)
    {
        CardMovedEvents.Add((boardId, card));
        return Task.CompletedTask;
    }
    public Task CommentCreatedAsync(int boardId, CardCommentDto comment)
    {
        CommentCreatedEvents.Add((boardId, comment));
        return Task.CompletedTask;
    }

    public Task CommentUpdatedAsync(int boardId, CardCommentDto comment)
    {
        CommentUpdatedEvents.Add((boardId, comment));
        return Task.CompletedTask;
    }

    public Task CommentDeletedAsync(int boardId, int cardId, int commentId)
    {
        CommentDeletedEvents.Add((boardId, cardId, commentId));
        return Task.CompletedTask;
    }

    public Task TagsChangedAsync(int boardId)
    {
        TagsChangedBoardIds.Add(boardId);
        PublishedEventNames.Add("tags-changed");
        return Task.CompletedTask;
    }

    public Task ResyncRequestedAsync(int boardId)
    {
        ResyncRequestedBoardIds.Add(boardId);
        PublishedEventNames.Add("resync-requested");
        return Task.CompletedTask;
    }

    public Task AttachmentAddedAsync(int boardId, int cardId, CardAttachmentDto attachment)
    {
        AttachmentAddedEvents.Add((boardId, cardId, attachment));
        PublishedEventNames.Add("attachment-added");
        return Task.CompletedTask;
    }

    public Task AttachmentDeletedAsync(int boardId, int cardId, int attachmentId)
    {
        AttachmentDeletedEvents.Add((boardId, cardId, attachmentId));
        PublishedEventNames.Add("attachment-deleted");
        return Task.CompletedTask;
    }

    public Task SystemInfoMessageUpdatedAsync(SystemInfoMessageDto? systemInfoMessage)
    {
        _ = systemInfoMessage;
        return Task.CompletedTask;
    }
}
