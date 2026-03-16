namespace BoardOil.Api.Realtime;

public sealed record TypingChangedEvent(
    int CardId,
    string UserLabel,
    bool IsTyping,
    DateTime ExpiresAtUtc);

public sealed record TypingPresenceEntry(
    int CardId,
    string UserLabel,
    DateTime ExpiresAtUtc);
