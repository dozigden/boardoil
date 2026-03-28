using BoardOil.Abstractions;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Column;

namespace BoardOil.Mcp.Server.Realtime;

public sealed class NoOpBoardEvents : IBoardEvents
{
    public Task ColumnCreatedAsync(int boardId, ColumnDto column) => Task.CompletedTask;
    public Task ColumnUpdatedAsync(int boardId, ColumnDto column) => Task.CompletedTask;
    public Task ColumnDeletedAsync(int boardId, int columnId) => Task.CompletedTask;

    public Task CardCreatedAsync(int boardId, CardDto card) => Task.CompletedTask;
    public Task CardUpdatedAsync(int boardId, CardDto card) => Task.CompletedTask;
    public Task CardDeletedAsync(int boardId, int cardId) => Task.CompletedTask;
    public Task CardMovedAsync(int boardId, CardDto card) => Task.CompletedTask;
}
