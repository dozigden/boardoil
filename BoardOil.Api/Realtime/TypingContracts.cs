namespace BoardOil.Api.Realtime;

public sealed record TypingChangedEvent(
    int CardId,
    string Field,
    string UserLabel,
    bool IsTyping,
    DateTime ExpiresAtUtc);

public sealed record TypingPresenceEntry(
    int CardId,
    string Field,
    string UserLabel,
    DateTime ExpiresAtUtc);
