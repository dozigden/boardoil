using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.Column;
using BoardOil.Ef.Repositories;
using BoardOil.Services.Board;
using BoardOil.Services.Card;
using BoardOil.Services.Column;
using BoardOil.Services.Tests.Infrastructure;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class BoardServiceTests : TestBaseDb
{
    [Fact]
    public async Task GetBoardAsync_WhenNoBoardExists_ShouldReturnInternalError()
    {
        // Act
        var service = CreateService();
        var result = await service.GetBoardAsync();

        // Assert
        Assert.False(result.Success);
        Assert.Equal(500, result.StatusCode);
        Assert.Equal("No board exists. Bootstrap has not run.", result.Message);
    }

    [Fact]
    public async Task GetBoardAsync_WhenBoardHasNoColumns_ShouldReturnEmptyColumns()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .Build();

        // Act
        var service = CreateService();
        var result = await service.GetBoardAsync();

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(board.BoardId, result.Data!.Id);
        Assert.Equal("BoardOil", result.Data.Name);
        Assert.Empty(result.Data.Columns);
    }

    [Fact]
    public async Task GetBoardAsync_WhenBoardHasColumnsAndCards_ShouldReturnOrderedHierarchy()
    {
        // Arrange
        CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("A")
            .AddCard("C")
            .AddCard("B", position: 1)
            .AddColumn("Doing")
            .AddCard("Done")
            .AddCard("In Progress", position: 0)
            .Build();

        // Act
        var service = CreateService();
        var result = await service.GetBoardAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("BoardOil", result.Data!.Name);
        Assert.Equal(2, result.Data.Columns.Count);

        var todo = result.Data.Columns[0];
        Assert.Equal("Todo", todo.Title);
        Assert.Equal(0, todo.Position);
        Assert.Equal(3, todo.Cards.Count);
        Assert.Equal("A", todo.Cards[0].Title);
        Assert.Equal(0, todo.Cards[0].Position);
        Assert.Equal("B", todo.Cards[1].Title);
        Assert.Equal(1, todo.Cards[1].Position);
        Assert.Equal("C", todo.Cards[2].Title);
        Assert.Equal(2, todo.Cards[2].Position);

        var doing = result.Data.Columns[1];
        Assert.Equal("Doing", doing.Title);
        Assert.Equal(1, doing.Position);
        Assert.Equal(2, doing.Cards.Count);
        Assert.Equal("In Progress", doing.Cards[0].Title);
        Assert.Equal(0, doing.Cards[0].Position);
        Assert.Equal("Done", doing.Cards[1].Title);
        Assert.Equal(1, doing.Cards[1].Position);
    }

    [Fact]
    public async Task GetBoardAsync_WhenCardsHaveTags_ShouldIncludeTagNames()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("A")
            .AddColumn("Doing")
            .Build();
        var cardId = board.GetCard("Todo", "A").Id;
        DbContextForArrange.CardTags.Add(new BoardOil.Ef.Entities.CardTag
        {
            CardId = cardId,
            TagName = "Bug"
        });
        await DbContextForArrange.SaveChangesAsync();

        // Act
        var service = CreateService();
        var result = await service.GetBoardAsync();

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var todoCard = result.Data!.Columns[0].Cards[0];
        Assert.Equal(["Bug"], todoCard.TagNames);
    }

    private BoardService CreateService()
    {
        var dbContext = CreateDbContextForAct();
        IBoardRepository boardRepository = new BoardRepository(dbContext);
        IColumnRepository columnRepository = new ColumnRepository(dbContext);
        ICardRepository cardRepository = new CardRepository(dbContext);
        return new BoardService(boardRepository, columnRepository, cardRepository);
    }
}
