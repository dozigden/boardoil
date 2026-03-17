using BoardOil.Abstractions;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Column;

namespace BoardOil.Services.Tests.Infrastructure;

public sealed class TestBoardEvents : IBoardEvents
{
    public Task ColumnCreatedAsync(ColumnDto column) => Task.CompletedTask;
    public Task ColumnUpdatedAsync(ColumnDto column) => Task.CompletedTask;
    public Task ColumnDeletedAsync(int columnId) => Task.CompletedTask;

    public Task CardCreatedAsync(CardDto card) => Task.CompletedTask;
    public Task CardUpdatedAsync(CardDto card) => Task.CompletedTask;
    public Task CardDeletedAsync(int cardId) => Task.CompletedTask;
    public Task CardMovedAsync(CardDto card) => Task.CompletedTask;
}
