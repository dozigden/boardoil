using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Realtime;

[Authorize(Policy = BoardOilPolicies.AuthenticatedUser)]
public sealed class BoardHub : Hub
{
    public Task SubscribeBoard(int boardId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, BoardHubGroupName.For(boardId));

    public Task UnsubscribeBoard(int boardId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, BoardHubGroupName.For(boardId));
}
