using BoardOil.Services.Contracts;

namespace BoardOil.Services.Abstractions;

public interface IBoardEvents
{
    Task ColumnCreatedAsync(ColumnDto column);
    Task ColumnUpdatedAsync(ColumnDto column);
    Task ColumnDeletedAsync(int columnId);

    Task CardCreatedAsync(CardDto card);
    Task CardUpdatedAsync(CardDto card);
    Task CardDeletedAsync(int cardId);
    Task CardMovedAsync(CardDto card);
}
