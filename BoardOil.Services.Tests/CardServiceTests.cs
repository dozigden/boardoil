using BoardOil.Ef;
using BoardOil.Ef.Entities;
using BoardOil.Services.Implementations;
using BoardOil.Services.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardServiceTests
{
    [Fact]
    public async Task UpdateCardAsync_WhenMovingCardToDifferentColumnWithOccupiedPosition_ShouldSucceed()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BoardOilDbContext>()
            .UseSqlite(connection)
            .Options;

        var cardToMoveId = await SeedBoardAsync(options);

        await using var dbContext = new BoardOilDbContext(options);
        var service = new CardService(dbContext, new CardValidator());

        var destinationColumnId = await dbContext.Columns
            .Where(c => c.Title == "Doing")
            .Select(c => c.Id)
            .SingleAsync();

        var result = await service.UpdateCardAsync(
            cardToMoveId,
            new UpdateCardRequest(
                BoardColumnId: destinationColumnId,
                Title: null,
                Description: null,
                Position: 1));

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(destinationColumnId, result.Data!.BoardColumnId);
        Assert.Equal(1, result.Data.Position);
    }

    private static async Task<int> SeedBoardAsync(DbContextOptions<BoardOilDbContext> options)
    {
        await using var db = new BoardOilDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = DateTime.UtcNow;
        var board = new Board
        {
            Name = "BoardOil",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var todo = new BoardColumn
        {
            Board = board,
            Title = "Todo",
            Position = 0,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var doing = new BoardColumn
        {
            Board = board,
            Title = "Doing",
            Position = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var movingCard = new BoardCard
        {
            BoardColumn = todo,
            Title = "Move me",
            Description = "source",
            SortKey = "50000000000000000000",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var destinationCard = new BoardCard
        {
            BoardColumn = doing,
            Title = "Existing",
            Description = "target",
            SortKey = "50000000000000000000",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.AddRange(board, todo, doing, movingCard, destinationCard);
        await db.SaveChangesAsync();

        return movingCard.Id;
    }
}
