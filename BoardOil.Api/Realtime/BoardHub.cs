using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using BoardOil.Api.Auth;
using BoardOil.Abstractions.Board;
using BoardOil.Services.Auth;

namespace BoardOil.Api.Realtime;

[Authorize(Policy = BoardOilPolicies.AuthenticatedUser)]
public sealed class BoardHub(IBoardAuthorisationService boardAuthorisationService) : Hub
{
    public async Task SubscribeBoard(int boardId)
    {
        if (Context.User is null || !Context.User.TryGetUserId(out var actorUserId))
        {
            throw new HubException("Invalid identity context.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardAccess);
        if (!hasPermission)
        {
            throw new HubException("You do not have access to this board.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, BoardHubGroupName.For(boardId));
    }

    public Task UnsubscribeBoard(int boardId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, BoardHubGroupName.For(boardId));
}
