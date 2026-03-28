using BoardOil.Contracts.Card;
using BoardOil.Contracts.Column;

namespace BoardOil.Contracts.Realtime;

public static class BoardRealtimeRelay
{
    public const string EndpointPath = "/api/internal/realtime/board-events";
    public const string ApiKeyHeaderName = "X-BoardOil-Internal-Key";
}

public static class BoardRealtimeEventTypes
{
    public const string ColumnCreated = "column_created";
    public const string ColumnUpdated = "column_updated";
    public const string ColumnDeleted = "column_deleted";
    public const string CardCreated = "card_created";
    public const string CardUpdated = "card_updated";
    public const string CardDeleted = "card_deleted";
    public const string CardMoved = "card_moved";
}

public sealed record BoardRealtimeRelayEvent(
    string EventType,
    int BoardId,
    ColumnDto? Column = null,
    int? ColumnId = null,
    CardDto? Card = null,
    int? CardId = null);
