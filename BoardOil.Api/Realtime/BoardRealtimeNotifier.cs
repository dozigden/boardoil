using BoardOil.Services.Abstractions;
using BoardOil.Services.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace BoardOil.Api.Realtime;

public sealed class BoardRealtimeNotifier(IHubContext<BoardHub> hubContext) : IBoardEvents
{
    public Task ColumnCreatedAsync(ColumnDto column) =>
        hubContext.Clients.All.SendAsync("ColumnCreated", column);

    public Task ColumnUpdatedAsync(ColumnDto column) =>
        hubContext.Clients.All.SendAsync("ColumnUpdated", column);

    public Task ColumnDeletedAsync(int columnId) =>
        hubContext.Clients.All.SendAsync("ColumnDeleted", columnId);

    public Task CardCreatedAsync(CardDto card) =>
        hubContext.Clients.All.SendAsync("CardCreated", card);

    public Task CardUpdatedAsync(CardDto card) =>
        hubContext.Clients.All.SendAsync("CardUpdated", card);

    public Task CardDeletedAsync(int cardId) =>
        hubContext.Clients.All.SendAsync("CardDeleted", cardId);

    public Task CardMovedAsync(CardDto card) =>
        hubContext.Clients.All.SendAsync("CardMoved", card);
}
