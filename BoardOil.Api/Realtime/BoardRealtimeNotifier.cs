using BoardOil.Abstractions;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Column;
using Microsoft.AspNetCore.SignalR;

namespace BoardOil.Api.Realtime;

public sealed class BoardRealtimeNotifier(
    IHubContext<BoardHub> hubContext,
    ILogger<BoardRealtimeNotifier> logger) : IBoardEvents
{
    public Task ColumnCreatedAsync(int boardId, ColumnDto column) =>
        TryPublishAsync(() => hubContext.Clients.Group(BoardHubGroupName.For(boardId)).SendAsync("ColumnCreated", column), nameof(ColumnCreatedAsync));

    public Task ColumnUpdatedAsync(int boardId, ColumnDto column) =>
        TryPublishAsync(() => hubContext.Clients.Group(BoardHubGroupName.For(boardId)).SendAsync("ColumnUpdated", column), nameof(ColumnUpdatedAsync));

    public Task ColumnDeletedAsync(int boardId, int columnId) =>
        TryPublishAsync(() => hubContext.Clients.Group(BoardHubGroupName.For(boardId)).SendAsync("ColumnDeleted", columnId), nameof(ColumnDeletedAsync));

    public Task CardCreatedAsync(int boardId, CardDto card) =>
        TryPublishAsync(() => hubContext.Clients.Group(BoardHubGroupName.For(boardId)).SendAsync("CardCreated", card), nameof(CardCreatedAsync));

    public Task CardUpdatedAsync(int boardId, CardDto card) =>
        TryPublishAsync(() => hubContext.Clients.Group(BoardHubGroupName.For(boardId)).SendAsync("CardUpdated", card), nameof(CardUpdatedAsync));

    public Task CardDeletedAsync(int boardId, int cardId) =>
        TryPublishAsync(() => hubContext.Clients.Group(BoardHubGroupName.For(boardId)).SendAsync("CardDeleted", cardId), nameof(CardDeletedAsync));

    public Task CardMovedAsync(int boardId, CardDto card) =>
        TryPublishAsync(() => hubContext.Clients.Group(BoardHubGroupName.For(boardId)).SendAsync("CardMoved", card), nameof(CardMovedAsync));

    private async Task TryPublishAsync(Func<Task> publish, string eventName)
    {
        try
        {
            await publish();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish realtime event {EventName}.", eventName);
        }
    }
}
