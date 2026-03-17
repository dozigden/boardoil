using BoardOil.Abstractions;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Column;
using Microsoft.AspNetCore.SignalR;

namespace BoardOil.Api.Realtime;

public sealed class BoardRealtimeNotifier(
    IHubContext<BoardHub> hubContext,
    ILogger<BoardRealtimeNotifier> logger) : IBoardEvents
{
    public Task ColumnCreatedAsync(ColumnDto column) =>
        TryPublishAsync(() => hubContext.Clients.All.SendAsync("ColumnCreated", column), nameof(ColumnCreatedAsync));

    public Task ColumnUpdatedAsync(ColumnDto column) =>
        TryPublishAsync(() => hubContext.Clients.All.SendAsync("ColumnUpdated", column), nameof(ColumnUpdatedAsync));

    public Task ColumnDeletedAsync(int columnId) =>
        TryPublishAsync(() => hubContext.Clients.All.SendAsync("ColumnDeleted", columnId), nameof(ColumnDeletedAsync));

    public Task CardCreatedAsync(CardDto card) =>
        TryPublishAsync(() => hubContext.Clients.All.SendAsync("CardCreated", card), nameof(CardCreatedAsync));

    public Task CardUpdatedAsync(CardDto card) =>
        TryPublishAsync(() => hubContext.Clients.All.SendAsync("CardUpdated", card), nameof(CardUpdatedAsync));

    public Task CardDeletedAsync(int cardId) =>
        TryPublishAsync(() => hubContext.Clients.All.SendAsync("CardDeleted", cardId), nameof(CardDeletedAsync));

    public Task CardMovedAsync(CardDto card) =>
        TryPublishAsync(() => hubContext.Clients.All.SendAsync("CardMoved", card), nameof(CardMovedAsync));

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
