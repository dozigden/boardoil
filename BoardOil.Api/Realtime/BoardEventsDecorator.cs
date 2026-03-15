using BoardOil.Services.Abstractions;
using BoardOil.Services.Contracts;

namespace BoardOil.Api.Realtime;

public sealed class BoardEventsDecorator(
    BoardRealtimeNotifier inner,
    ILogger<BoardEventsDecorator> logger) : IBoardEvents
{
    public Task ColumnCreatedAsync(ColumnDto column) =>
        TryPublishAsync(() => inner.ColumnCreatedAsync(column), nameof(ColumnCreatedAsync));

    public Task ColumnUpdatedAsync(ColumnDto column) =>
        TryPublishAsync(() => inner.ColumnUpdatedAsync(column), nameof(ColumnUpdatedAsync));

    public Task ColumnDeletedAsync(int columnId) =>
        TryPublishAsync(() => inner.ColumnDeletedAsync(columnId), nameof(ColumnDeletedAsync));

    public Task CardCreatedAsync(CardDto card) =>
        TryPublishAsync(() => inner.CardCreatedAsync(card), nameof(CardCreatedAsync));

    public Task CardUpdatedAsync(CardDto card) =>
        TryPublishAsync(() => inner.CardUpdatedAsync(card), nameof(CardUpdatedAsync));

    public Task CardDeletedAsync(int cardId) =>
        TryPublishAsync(() => inner.CardDeletedAsync(cardId), nameof(CardDeletedAsync));

    public Task CardMovedAsync(CardDto card) =>
        TryPublishAsync(() => inner.CardMovedAsync(card), nameof(CardMovedAsync));

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
