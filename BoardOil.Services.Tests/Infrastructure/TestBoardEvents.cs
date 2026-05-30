using BoardOil.Abstractions;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Configuration;
using BoardOil.Contracts.Column;

namespace BoardOil.Services.Tests.Infrastructure;

public sealed class TestBoardEvents : IBoardEvents
{
    public readonly List<(int BoardId, CardCommentDto Comment)> CommentCreatedEvents = [];
    public readonly List<int> ResyncRequestedBoardIds = [];

    public Task ColumnCreatedAsync(int boardId, ColumnDto column) => Task.CompletedTask;
    public Task ColumnUpdatedAsync(int boardId, ColumnDto column) => Task.CompletedTask;
    public Task ColumnDeletedAsync(int boardId, int columnId) => Task.CompletedTask;

    public Task CardCreatedAsync(int boardId, CardDto card) => Task.CompletedTask;
    public Task CardUpdatedAsync(int boardId, CardDto card) => Task.CompletedTask;
    public Task CardDeletedAsync(int boardId, int cardId) => Task.CompletedTask;
    public Task CardMovedAsync(int boardId, CardDto card) => Task.CompletedTask;
    public Task CommentCreatedAsync(int boardId, CardCommentDto comment)
    {
        CommentCreatedEvents.Add((boardId, comment));
        return Task.CompletedTask;
    }

    public Task ResyncRequestedAsync(int boardId)
    {
        ResyncRequestedBoardIds.Add(boardId);
        return Task.CompletedTask;
    }

    public Task SystemInfoMessageUpdatedAsync(SystemInfoMessageDto? systemInfoMessage)
    {
        _ = systemInfoMessage;
        return Task.CompletedTask;
    }
}
