using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Column;
using BoardOil.Contracts.Column;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef.Repositories;
using BoardOil.Services.Board;
using BoardOil.Services.Column;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class ColumnServiceTests : TestBaseDb
{
    [Fact]
    public async Task GetColumnsAsync_WhenNoBoardExists_ShouldReturnNotFound()
    {
        var service = CreateService();
        var result = await service.GetColumnsAsync(1, ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Board not found.", result.Message);
    }

    [Fact]
    public async Task GetColumnsAsync_WhenBoardHasColumns_ShouldReturnOrderedColumns()
    {
        CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .AddColumn("Done")
            .Build();

        var service = CreateService();
        var result = await service.GetColumnsAsync(1, ActorUserId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data!.Count);
        Assert.Equal("Todo", result.Data[0].Title);
        Assert.Equal("Doing", result.Data[1].Title);
        Assert.Equal("Done", result.Data[2].Title);
        Assert.False(string.IsNullOrWhiteSpace(result.Data[0].SortKey));
        Assert.False(string.IsNullOrWhiteSpace(result.Data[1].SortKey));
        Assert.False(string.IsNullOrWhiteSpace(result.Data[2].SortKey));
    }

    [Fact]
    public async Task CreateColumnAsync_ShouldAppendToEnd()
    {
        CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();

        var service = CreateService();
        var result = await service.CreateColumnAsync(1, new CreateColumnRequest("Done"), ActorUserId);

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("Done", result.Data!.Title);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.SortKey));
        var titles = await GetOrderedColumnTitlesAsync();

        Assert.Equal(["Todo", "Doing", "Done"], titles);
    }

    [Fact]
    public async Task CreateColumnAsync_WhenNoBoardExists_ShouldReturnNotFound()
    {
        var service = CreateService();
        var result = await service.CreateColumnAsync(1, new CreateColumnRequest("Todo"), ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Board not found.", result.Message);
    }

    [Fact]
    public async Task CreateColumnAsync_WhenTitleIsWhitespace_ShouldReturnValidationErrorForTitle()
    {
        CreateBoard("BoardOil").Build();

        var service = CreateService();
        var result = await service.CreateColumnAsync(1, new CreateColumnRequest("   "), ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("title"));
    }

    [Fact]
    public async Task CreateColumnAsync_WhenTitleTooLong_ShouldReturnValidationErrorForTitle()
    {
        CreateBoard("BoardOil").Build();
        var longTitle = new string('A', 201);

        var service = CreateService();
        var result = await service.CreateColumnAsync(1, new CreateColumnRequest(longTitle), ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("title"));
    }

    [Fact]
    public async Task CreateColumnAsync_WhenTitleHasInvalidCharacters_ShouldReturnValidationErrorForTitle()
    {
        CreateBoard("BoardOil").Build();

        var service = CreateService();
        var result = await service.CreateColumnAsync(1, new CreateColumnRequest("bad@title"), ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("title"));
    }

    [Fact]
    public async Task UpdateColumnAsync_WhenColumnMissing_ShouldReturnNotFound()
    {
        CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();

        var service = CreateService();
        var result = await service.UpdateColumnAsync(1, 999_999, new UpdateColumnRequest("X"), ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Column not found.", result.Message);
    }

    [Fact]
    public async Task UpdateColumnAsync_WhenNoBoardExists_ShouldReturnNotFound()
    {
        var service = CreateService();
        var result = await service.UpdateColumnAsync(1, 1, new UpdateColumnRequest("X"), ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Board not found.", result.Message);
    }

    [Fact]
    public async Task UpdateColumnAsync_WhenUpdatingTitleOnly_ShouldPersistTrimmedTitle()
    {
        var board = CreateBoard("BoardOil")
            .AddColumn("Old")
            .Build();
        var columnId = board.GetColumn("Old").Id;

        var service = CreateService();
        var result = await service.UpdateColumnAsync(1, columnId, new UpdateColumnRequest("  New Title  "), ActorUserId);

        Assert.True(result.Success);
        Assert.Equal("New Title", result.Data!.Title);
        var stored = await DbContextForAssert.Columns.SingleAsync(x => x.Id == columnId);

        Assert.Equal("New Title", stored.Title);
        Assert.False(string.IsNullOrWhiteSpace(stored.SortKey));
    }

    [Fact]
    public async Task ReorderColumnsAsync_ShouldPersistCompleteOrderAndPreserveCards()
    {
        // Arrange
        var board = CreateBoard().AddColumn("A").AddCard("First").AddCard("Second")
            .AddColumn("B").AddColumn("C").Build();
        var otherBoard = CreateBoard("Other").AddColumn("Other column").Build();
        var beforeCards = await DbContextForAssert.Cards.AsNoTracking()
            .OrderBy(card => card.Id).Select(card => new { card.Id, card.BoardColumnId, card.SortKey }).ToListAsync();
        var otherColumn = await DbContextForAssert.Columns.AsNoTracking().SingleAsync(column => column.BoardId == otherBoard.BoardId);
        int[] requested = [board.GetColumn("C").Id, board.GetColumn("A").Id, board.GetColumn("B").Id];

        // Act
        var result = await CreateService().ReorderColumnsAsync(board.BoardId, new(requested), ActorUserId);

        // Assert
        Assert.True(result.Success, result.Message);
        Assert.Equal(requested, result.Data!.Select(column => column.Id));
        var stored = await DbContextForAssert.Columns.AsNoTracking().Where(column => column.BoardId == board.BoardId)
            .OrderBy(column => column.SortKey).ToListAsync();
        Assert.Equal(requested, stored.Select(column => column.Id));
        Assert.Equal(result.Data!.Select(column => column.SortKey), stored.Select(column => column.SortKey));
        Assert.Equal(beforeCards, await DbContextForAssert.Cards.AsNoTracking()
            .OrderBy(card => card.Id).Select(card => new { card.Id, card.BoardColumnId, card.SortKey }).ToListAsync());
        Assert.Equal(otherColumn.SortKey, (await DbContextForAssert.Columns.AsNoTracking()
            .SingleAsync(column => column.BoardId == otherBoard.BoardId)).SortKey);
        var notification = Assert.Single(Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>()).ColumnsReorderedEvents);
        Assert.Equal(board.BoardId, notification.BoardId);
        Assert.Equal(result.Data, notification.Columns);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("foreign")]
    [InlineData("nonpositive")]
    [InlineData("null")]
    public async Task ReorderColumnsAsync_WhenIdSetIsInvalid_ShouldPreserveOrderAndReturnValidation(string invalid)
    {
        // Arrange
        var board = CreateBoard().AddColumn("A").AddColumn("B").Build();
        var other = CreateBoard("Other").AddColumn("Foreign").Build();
        var first = board.GetColumn("A").Id;
        IReadOnlyList<int>? ids = invalid switch
        {
            "missing" => [first],
            "duplicate" => [first, first],
            "unknown" => [first, int.MaxValue],
            "foreign" => [first, other.GetColumn("Foreign").Id],
            "nonpositive" => [first, 0],
            _ => null
        };
        var original = await DbContextForAssert.Columns.AsNoTracking().OrderBy(column => column.Id)
            .Select(column => new { column.Id, column.SortKey, column.UpdatedAtUtc }).ToListAsync();

        // Act
        var result = await CreateService().ReorderColumnsAsync(board.BoardId, new(ids), ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("columnIds", result.ValidationErrors!.Keys);
        Assert.Equal(original, await DbContextForAssert.Columns.AsNoTracking().OrderBy(column => column.Id)
            .Select(column => new { column.Id, column.SortKey, column.UpdatedAtUtc }).ToListAsync());
        Assert.Empty(Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>()).ColumnsReorderedEvents);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReorderColumnsAsync_WhenMembershipChanged_ShouldRejectStaleOrder(bool added)
    {
        // Arrange
        var board = CreateBoard().AddColumn("A").AddColumn("B").Build();
        int[] staleIds = [board.GetColumn("B").Id, board.GetColumn("A").Id];
        var service = CreateService();
        if (added)
        {
            Assert.True((await service.CreateColumnAsync(board.BoardId, new("C"), ActorUserId)).Success);
        }
        else
        {
            Assert.True((await service.DeleteColumnAsync(board.BoardId, board.GetColumn("A").Id, ActorUserId)).Success);
        }
        var current = (await service.GetColumnsAsync(board.BoardId, ActorUserId)).Data!;

        // Act
        var result = await service.ReorderColumnsAsync(board.BoardId, new(staleIds), ActorUserId);

        // Assert
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(current, (await service.GetColumnsAsync(board.BoardId, ActorUserId)).Data);
        Assert.Empty(Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>()).ColumnsReorderedEvents);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReorderColumnsAsync_WhenOrderAlreadyMatches_ShouldLeaveKeysAndTimestampsUnchanged(bool empty)
    {
        // Arrange
        var builder = CreateBoard();
        if (!empty)
        {
            builder.AddColumn("A").AddColumn("B").AddColumn("C");
        }
        var board = builder.Build();
        var service = CreateService();
        var before = (await service.GetColumnsAsync(board.BoardId, ActorUserId)).Data!;

        // Act
        var result = await service.ReorderColumnsAsync(board.BoardId, new(before.Select(column => column.Id).ToArray()), ActorUserId);

        // Assert
        Assert.True(result.Success, result.Message);
        Assert.Equal(before, result.Data);
        Assert.Equal(before, (await service.GetColumnsAsync(board.BoardId, ActorUserId)).Data);
        Assert.Empty(Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>()).ColumnsReorderedEvents);
    }

    [Fact]
    public async Task ReorderColumnsAsync_WhenNotOwner_ShouldReturnForbidden()
    {
        // Arrange
        var board = CreateBoard().AddColumn("A").AddColumn("B").Build();
        var membership = await DbContextForArrange.BoardMembers.SingleAsync();
        membership.Role = BoardMemberRole.Contributor;
        await DbContextForArrange.SaveChangesAsync();

        // Act
        var result = await CreateService().ReorderColumnsAsync(board.BoardId,
            new([board.GetColumn("B").Id, board.GetColumn("A").Id]), ActorUserId);

        // Assert
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(["A", "B"], await GetOrderedColumnTitlesAsync());
        Assert.Empty(Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>()).ColumnsReorderedEvents);
    }

    [Fact]
    public async Task ReorderColumnsAsync_WhenBoardMissing_ShouldReturnNotFound()
    {
        var result = await CreateService().ReorderColumnsAsync(int.MaxValue, new([]), ActorUserId);

        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task UpdateColumnAsync_WhenTitleInvalid_ShouldReturnValidationErrorForTitle()
    {
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var columnId = board.GetColumn("Todo").Id;

        var service = CreateService();
        var result = await service.UpdateColumnAsync(1, columnId, new UpdateColumnRequest("bad@title"), ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("title"));
    }

    [Fact]
    public async Task DeleteColumnAsync_WhenColumnExists_ShouldRemoveAndReorderRemaining()
    {
        var board = CreateBoard("BoardOil")
            .AddColumn("A")
            .AddColumn("B")
            .AddColumn("C")
            .Build();
        var deletingId = board.GetColumn("B").Id;

        var service = CreateService();
        var result = await service.DeleteColumnAsync(1, deletingId, ActorUserId);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        var titles = await GetOrderedColumnTitlesAsync();

        Assert.Equal(["A", "C"], titles);
        var sortKeys = await DbContextForAssert.Columns
            .OrderBy(x => x.SortKey)
            .Select(x => x.SortKey)
            .ToListAsync();
        Assert.Equal(2, sortKeys.Count);
        Assert.All(sortKeys, key => Assert.False(string.IsNullOrWhiteSpace(key)));
        var events = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
        Assert.Equal((board.BoardId, deletingId), Assert.Single(events.ColumnDeletedEvents));
    }

    [Fact]
    public async Task DeleteColumnAsync_WhenColumnHasCards_ShouldReturnValidationAndPreserveCardsAndComments()
    {
        // Arrange
        var board = CreateBoard().AddColumn("Todo").AddCard("Card", "Keep this content").Build();
        var columnId = board.GetColumn("Todo").Id;
        var card = board.GetCard("Card");
        DbContextForArrange.CardComments.Add(new EntityCardComment
        {
            CardId = card.Id,
            AuthorUserId = ActorUserId,
            Text = "Keep this comment"
        });
        await DbContextForArrange.SaveChangesAsync();
        var service = CreateService();

        // Act
        var result = await service.DeleteColumnAsync(board.BoardId, columnId, ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Column must be empty before it can be deleted.", result.Message);
        Assert.Equal([result.Message], result.ValidationErrors!["id"]);
        Assert.Equal(columnId, (await DbContextForAssert.Columns.SingleAsync()).Id);
        var storedCard = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(card.Id, storedCard.Id);
        Assert.Equal(columnId, storedCard.BoardColumnId);
        Assert.Equal("Keep this content", storedCard.Description);
        Assert.Equal("Keep this comment", (await DbContextForAssert.CardComments.SingleAsync()).Text);
        var events = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
        Assert.Empty(events.ColumnDeletedEvents);
        Assert.Empty(events.CardDeletedEvents);
    }

    [Fact]
    public async Task DeleteColumnAsync_WhenColumnMissing_ShouldReturnOk()
    {
        CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();

        var service = CreateService();
        var result = await service.DeleteColumnAsync(1, 999_999, ActorUserId);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task DeleteColumnAsync_WhenColumnBelongsToDifferentBoard_ShouldReturnNotFound()
    {
        var boardOne = CreateBoard("Board One")
            .AddColumn("Todo")
            .Build();
        var boardTwo = CreateBoard("Board Two")
            .AddColumn("Other")
            .Build();
        var boardTwoColumnId = boardTwo.GetColumn("Other").Id;

        var service = CreateService();
        var result = await service.DeleteColumnAsync(boardOne.BoardId, boardTwoColumnId, ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Column not found.", result.Message);
    }

    [Fact]
    public async Task DeleteColumnAsync_WhenNoBoardExists_ShouldReturnNotFound()
    {
        var service = CreateService();
        var result = await service.DeleteColumnAsync(1, 1, ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Board not found.", result.Message);
    }

    private async Task<List<string>> GetOrderedColumnTitlesAsync() =>
        await DbContextForAssert.Columns
            .OrderBy(x => x.SortKey)
            .Select(x => x.Title)
            .ToListAsync();

    private ColumnService CreateService()
    {
        return ResolveService<ColumnService>();
    }
}
