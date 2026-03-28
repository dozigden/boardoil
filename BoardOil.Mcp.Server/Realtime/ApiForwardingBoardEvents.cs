using BoardOil.Abstractions;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Column;
using BoardOil.Contracts.Realtime;
using System.Net.Http.Json;

namespace BoardOil.Mcp.Server.Realtime;

public sealed class ApiForwardingBoardEvents(
    HttpClient httpClient,
    string? apiKey) : IBoardEvents
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly string? _apiKey = apiKey;

    public Task ColumnCreatedAsync(int boardId, ColumnDto column) =>
        ForwardAsync(new BoardRealtimeRelayEvent(BoardRealtimeEventTypes.ColumnCreated, boardId, Column: column));

    public Task ColumnUpdatedAsync(int boardId, ColumnDto column) =>
        ForwardAsync(new BoardRealtimeRelayEvent(BoardRealtimeEventTypes.ColumnUpdated, boardId, Column: column));

    public Task ColumnDeletedAsync(int boardId, int columnId) =>
        ForwardAsync(new BoardRealtimeRelayEvent(BoardRealtimeEventTypes.ColumnDeleted, boardId, ColumnId: columnId));

    public Task CardCreatedAsync(int boardId, CardDto card) =>
        ForwardAsync(new BoardRealtimeRelayEvent(BoardRealtimeEventTypes.CardCreated, boardId, Card: card));

    public Task CardUpdatedAsync(int boardId, CardDto card) =>
        ForwardAsync(new BoardRealtimeRelayEvent(BoardRealtimeEventTypes.CardUpdated, boardId, Card: card));

    public Task CardDeletedAsync(int boardId, int cardId) =>
        ForwardAsync(new BoardRealtimeRelayEvent(BoardRealtimeEventTypes.CardDeleted, boardId, CardId: cardId));

    public Task CardMovedAsync(int boardId, CardDto card) =>
        ForwardAsync(new BoardRealtimeRelayEvent(BoardRealtimeEventTypes.CardMoved, boardId, Card: card));

    private async Task ForwardAsync(BoardRealtimeRelayEvent relayEvent)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, BoardRealtimeRelay.EndpointPath);
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                request.Headers.TryAddWithoutValidation(BoardRealtimeRelay.ApiKeyHeaderName, _apiKey);
            }

            request.Content = JsonContent.Create(relayEvent);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine(
                    $"BoardOil MCP event forward failed: {(int)response.StatusCode} {response.ReasonPhrase} for '{relayEvent.EventType}'.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"BoardOil MCP event forward threw '{ex.GetType().Name}' for '{relayEvent.EventType}': {ex.Message}");
        }
    }
}
