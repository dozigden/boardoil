using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class FluentBoardBuilderTests : TestBaseDb
{
    [Fact]
    public async Task AddCard_WhenDescriptionOmitted_ShouldDefaultToEmptyString()
    {
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("No Description")
            .Build();

        var cardId = board.GetCard("No Description").Id;
        var stored = await DbContextForAssert.Cards.SingleAsync(x => x.Id == cardId);

        Assert.Equal(string.Empty, stored.Description);
    }

    [Fact]
    public void GetCard_WhenColumnNotSpecified_ShouldUseCurrentColumn()
    {
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Card A");

        var byCurrentColumn = board.GetCard("Card A");
        var byNamedColumn = board.GetCard("Todo", "Card A");

        Assert.Same(byNamedColumn, byCurrentColumn);
    }

    [Fact]
    public async Task Build_ShouldPersistQueuedEntities()
    {
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Card A");

        Assert.Equal(0, await DbContextForAssert.Columns.CountAsync());
        Assert.Equal(0, await DbContextForAssert.Cards.CountAsync());

        board.Build();

        Assert.Equal(1, await DbContextForAssert.Columns.CountAsync());
        Assert.Equal(1, await DbContextForAssert.Cards.CountAsync());
    }

    [Fact]
    public async Task AddCard_WhenInsertedAtPosition_ShouldPreserveSortOrderAfterBuild()
    {
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("A")
            .AddCard("C")
            .AddCard("B", position: 1)
            .Build();

        var todoColumnId = board.GetColumn("Todo").Id;
        var titles = await GetOrderedTitlesAsync(DbContextForAssert, todoColumnId);

        Assert.Equal(["A", "B", "C"], titles);
    }
}
