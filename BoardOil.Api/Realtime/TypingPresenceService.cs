using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace BoardOil.Api.Realtime;

public sealed class TypingPresenceService(
    IHubContext<BoardHub> hubContext,
    BoardOil.Api.Configuration.BoardOilRuntimeOptions runtimeOptions,
    TimeProvider timeProvider) : ITypingPresenceService
{
    private readonly ConcurrentDictionary<string, TypingPresenceEntry> _entries = new();

    public async Task StartTypingAsync(int cardId, string userLabel, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(cardId, userLabel);
        if (normalized is null)
        {
            return;
        }

        var expiresAtUtc = timeProvider.GetUtcNow().AddSeconds(runtimeOptions.TypingTtlSeconds).UtcDateTime;
        var entry = new TypingPresenceEntry(normalized.Value.CardId, normalized.Value.UserLabel, expiresAtUtc);
        _entries[ComposeKey(entry)] = entry;

        await hubContext.Clients.All.SendAsync(
            "TypingChanged",
            new TypingChangedEvent(entry.CardId, entry.UserLabel, true, entry.ExpiresAtUtc),
            cancellationToken);
    }

    public async Task StopTypingAsync(int cardId, string userLabel, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(cardId, userLabel);
        if (normalized is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entry = new TypingPresenceEntry(normalized.Value.CardId, normalized.Value.UserLabel, now);
        _entries.TryRemove(ComposeKey(entry), out _);

        await hubContext.Clients.All.SendAsync(
            "TypingChanged",
            new TypingChangedEvent(entry.CardId, entry.UserLabel, false, now),
            cancellationToken);
    }

    public async Task SweepExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expired = _entries.Values.Where(x => x.ExpiresAtUtc <= now).ToList();

        foreach (var entry in expired)
        {
            var removed = _entries.TryRemove(new KeyValuePair<string, TypingPresenceEntry>(ComposeKey(entry), entry));
            if (!removed)
            {
                continue;
            }

            await hubContext.Clients.All.SendAsync(
                "TypingChanged",
                new TypingChangedEvent(entry.CardId, entry.UserLabel, false, now),
                cancellationToken);
        }
    }

    public IReadOnlyList<TypingPresenceEntry> GetActiveEntries()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return _entries.Values
            .Where(x => x.ExpiresAtUtc > now)
            .OrderBy(x => x.CardId)
            .ThenBy(x => x.UserLabel)
            .ToList();
    }

    private static string ComposeKey(TypingPresenceEntry entry) =>
        $"{entry.CardId}:{entry.UserLabel}";

    private static (int CardId, string UserLabel)? Normalize(int cardId, string userLabel)
    {
        if (cardId <= 0)
        {
            return null;
        }

        var normalizedLabel = userLabel?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedLabel))
        {
            return null;
        }

        return (cardId, normalizedLabel);
    }
}
