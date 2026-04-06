using BoardOil.Contracts.Card;
using BoardOil.Contracts.Column;

namespace BoardOil.Abstractions;

public interface IBoardEvents
{
    Task ColumnCreatedAsync(int boardId, ColumnDto column);
    Task ColumnUpdatedAsync(int boardId, ColumnDto column);
    Task ColumnDeletedAsync(int boardId, int columnId);

    Task CardCreatedAsync(int boardId, CardDto card);
    Task CardUpdatedAsync(int boardId, CardDto card);
    Task CardDeletedAsync(int boardId, int cardId);
    Task CardMovedAsync(int boardId, CardDto card);

    Task ResyncRequestedAsync(int boardId);
}
