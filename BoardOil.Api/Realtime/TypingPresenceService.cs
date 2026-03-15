using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace BoardOil.Api.Realtime;

public sealed class TypingPresenceService(
    IHubContext<BoardHub> hubContext,
    BoardOil.Api.Configuration.BoardOilRuntimeOptions runtimeOptions) : ITypingPresenceService
{
    private readonly ConcurrentDictionary<string, TypingPresenceEntry> _entries = new();

    public async Task StartTypingAsync(int cardId, string field, string userLabel, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(cardId, field, userLabel);
        if (normalized is null)
        {
            return;
        }

        var expiresAtUtc = DateTime.UtcNow.AddSeconds(runtimeOptions.TypingTtlSeconds);
        var entry = new TypingPresenceEntry(normalized.Value.CardId, normalized.Value.Field, normalized.Value.UserLabel, expiresAtUtc);
        _entries[ComposeKey(entry)] = entry;

        await hubContext.Clients.All.SendAsync(
            "TypingChanged",
            new TypingChangedEvent(entry.CardId, entry.Field, entry.UserLabel, true, entry.ExpiresAtUtc),
            cancellationToken);
    }

    public async Task StopTypingAsync(int cardId, string field, string userLabel, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(cardId, field, userLabel);
        if (normalized is null)
        {
            return;
        }

        var entry = new TypingPresenceEntry(normalized.Value.CardId, normalized.Value.Field, normalized.Value.UserLabel, DateTime.UtcNow);
        _entries.TryRemove(ComposeKey(entry), out _);

        await hubContext.Clients.All.SendAsync(
            "TypingChanged",
            new TypingChangedEvent(entry.CardId, entry.Field, entry.UserLabel, false, DateTime.UtcNow),
            cancellationToken);
    }

    public async Task SweepExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expired = _entries.Values.Where(x => x.ExpiresAtUtc <= now).ToList();

        foreach (var entry in expired)
        {
            _entries.TryRemove(ComposeKey(entry), out _);
            await hubContext.Clients.All.SendAsync(
                "TypingChanged",
                new TypingChangedEvent(entry.CardId, entry.Field, entry.UserLabel, false, now),
                cancellationToken);
        }
    }

    public IReadOnlyList<TypingPresenceEntry> GetActiveEntries()
    {
        var now = DateTime.UtcNow;
        return _entries.Values
            .Where(x => x.ExpiresAtUtc > now)
            .OrderBy(x => x.CardId)
            .ThenBy(x => x.Field)
            .ThenBy(x => x.UserLabel)
            .ToList();
    }

    private static string ComposeKey(TypingPresenceEntry entry) =>
        $"{entry.CardId}:{entry.Field}:{entry.UserLabel}";

    private static (int CardId, string Field, string UserLabel)? Normalize(int cardId, string field, string userLabel)
    {
        if (cardId <= 0)
        {
            return null;
        }

        var normalizedField = field?.Trim();
        var normalizedLabel = userLabel?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedField) || string.IsNullOrWhiteSpace(normalizedLabel))
        {
            return null;
        }

        return (cardId, normalizedField, normalizedLabel);
    }
}
