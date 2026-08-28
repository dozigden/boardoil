using BoardOil.Abstractions;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Configuration;
using BoardOil.Contracts.Column;

namespace BoardOil.Services.Tests.Infrastructure;

public sealed class TestBoardEvents : IBoardEvents
{
    public readonly List<(int BoardId, CardCommentDto Comment)> CommentCreatedEvents = [];
    public readonly List<(int BoardId, CardDto Card)> CardCreatedEvents = [];
    public readonly List<(int BoardId, CardDto Card)> CardUpdatedEvents = [];
    public readonly List<(int BoardId, CardDto Card)> CardMovedEvents = [];
    public readonly List<(int BoardId, int CardId)> CardDeletedEvents = [];
    public readonly List<int> ResyncRequestedBoardIds = [];
    public readonly List<string> PublishedEventNames = [];

    public Task ColumnCreatedAsync(int boardId, ColumnDto column) => Task.CompletedTask;
    public Task ColumnUpdatedAsync(int boardId, ColumnDto column) => Task.CompletedTask;
    public Task ColumnDeletedAsync(int boardId, int columnId) => Task.CompletedTask;

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

    public Task ResyncRequestedAsync(int boardId)
    {
        ResyncRequestedBoardIds.Add(boardId);
        PublishedEventNames.Add("resync-requested");
        return Task.CompletedTask;
    }

    public Task SystemInfoMessageUpdatedAsync(SystemInfoMessageDto? systemInfoMessage)
    {
        _ = systemInfoMessage;
        return Task.CompletedTask;
    }
}
