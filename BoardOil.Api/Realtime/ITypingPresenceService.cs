namespace BoardOil.Api.Realtime;

public interface ITypingPresenceService
{
    Task StartTypingAsync(int cardId, string userLabel, CancellationToken cancellationToken = default);
    Task StopTypingAsync(int cardId, string userLabel, CancellationToken cancellationToken = default);
    Task SweepExpiredAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<TypingPresenceEntry> GetActiveEntries();
}
