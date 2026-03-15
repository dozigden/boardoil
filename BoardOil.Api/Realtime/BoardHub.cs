using Microsoft.AspNetCore.SignalR;

namespace BoardOil.Api.Realtime;

public sealed class BoardHub(ITypingPresenceService typingPresenceService) : Hub
{
    public async Task TypingStarted(int cardId, string field, string userLabel)
    {
        await typingPresenceService.StartTypingAsync(cardId, field, userLabel, Context.ConnectionAborted);
    }

    public async Task TypingStopped(int cardId, string field, string userLabel)
    {
        await typingPresenceService.StopTypingAsync(cardId, field, userLabel, Context.ConnectionAborted);
    }

    public override async Task OnConnectedAsync()
    {
        var activeEntries = typingPresenceService.GetActiveEntries();
        foreach (var entry in activeEntries)
        {
            await Clients.Caller.SendAsync(
                "TypingChanged",
                new TypingChangedEvent(entry.CardId, entry.Field, entry.UserLabel, true, entry.ExpiresAtUtc),
                Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }
}
