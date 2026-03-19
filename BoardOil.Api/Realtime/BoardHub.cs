using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using BoardOil.Services.Auth;
using System.Security.Claims;

namespace BoardOil.Api.Realtime;

[Authorize(Policy = BoardOilPolicies.AuthenticatedUser)]
public sealed class BoardHub(ITypingPresenceService typingPresenceService) : Hub
{
    public async Task TypingStarted(int cardId)
    {
        var userLabel = ResolveUserLabel(Context.User);
        if (string.IsNullOrWhiteSpace(userLabel))
        {
            return;
        }

        await typingPresenceService.StartTypingAsync(cardId, userLabel, Context.ConnectionAborted);
    }

    public async Task TypingStopped(int cardId)
    {
        var userLabel = ResolveUserLabel(Context.User);
        if (string.IsNullOrWhiteSpace(userLabel))
        {
            return;
        }

        await typingPresenceService.StopTypingAsync(cardId, userLabel, Context.ConnectionAborted);
    }

    public override async Task OnConnectedAsync()
    {
        var activeEntries = typingPresenceService.GetActiveEntries();
        foreach (var entry in activeEntries)
        {
            await Clients.Caller.SendAsync(
                "TypingChanged",
                new TypingChangedEvent(entry.CardId, entry.UserLabel, true, entry.ExpiresAtUtc),
                Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }

    private static string? ResolveUserLabel(ClaimsPrincipal? user)
    {
        if (user is null)
        {
            return null;
        }

        var fromName = user.FindFirstValue(ClaimTypes.Name)?.Trim();
        if (!string.IsNullOrWhiteSpace(fromName))
        {
            return fromName;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)?.Trim();
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return $"User-{userId}";
        }

        return null;
    }
}
