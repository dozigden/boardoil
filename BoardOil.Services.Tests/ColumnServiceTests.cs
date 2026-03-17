using BoardOil.Services.Abstractions;
using BoardOil.Services.Board;
using BoardOil.Services.Column;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class ColumnServiceTests : TestBaseDb
{
    [Fact]
    public async Task GetColumnsAsync_WhenNoBoardExists_ShouldReturnInternalError()
    {
        var service = CreateService();
        var result = await service.GetColumnsAsync();

        Assert.False(result.Success);
        Assert.Equal(500, result.StatusCode);
        Assert.Equal("No board exists. Bootstrap has not run.", result.Message);
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
        var result = await service.GetColumnsAsync();

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data!.Count);
        Assert.Equal("Todo", result.Data[0].Title);
        Assert.Equal(0, result.Data[0].Position);
        Assert.Equal("Doing", result.Data[1].Title);
        Assert.Equal(1, result.Data[1].Position);
        Assert.Equal("Done", result.Data[2].Title);
        Assert.Equal(2, result.Data[2].Position);
    }

    [Fact]
    public async Task CreateColumnAsync_WhenPositionIsNull_ShouldAppendToEnd()
    {
        CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();

        var service = CreateService();
        var result = await service.CreateColumnAsync(new CreateColumnRequest("Done", null));

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("Done", result.Data!.Title);
        Assert.Equal(2, result.Data.Position);
        var titles = await GetOrderedColumnTitlesAsync();

        Assert.Equal(["Todo", "Doing", "Done"], titles);
    }

    [Fact]
    public async Task CreateColumnAsync_WhenPositionIsZero_ShouldInsertAtStart()
    {
        CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();

        var service = CreateService();
        var result = await service.CreateColumnAsync(new CreateColumnRequest("Backlog", 0));

        Assert.True(result.Success);
        Assert.Equal(0, result.Data!.Position);
        var titles = await GetOrderedColumnTitlesAsync();

        Assert.Equal(["Backlog", "Todo", "Doing"], titles);
    }

    [Fact]
    public async Task CreateColumnAsync_WhenPositionIsMiddle_ShouldInsertInMiddle()
    {
        CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Done")
            .Build();

        var service = CreateService();
        var result = await service.CreateColumnAsync(new CreateColumnRequest("Doing", 1));

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.Position);
        var titles = await GetOrderedColumnTitlesAsync();

        Assert.Equal(["Todo", "Doing", "Done"], titles);
    }

    [Fact]
    public async Task CreateColumnAsync_WhenNoBoardExists_ShouldReturnInternalError()
    {
        var service = CreateService();
        var result = await service.CreateColumnAsync(new CreateColumnRequest("Todo", null));

        Assert.False(result.Success);
        Assert.Equal(500, result.StatusCode);
        Assert.Equal("No board exists. Bootstrap has not run.", result.Message);
    }

    [Fact]
    public async Task CreateColumnAsync_WhenTitleIsWhitespace_ShouldReturnValidationErrorForTitle()
    {
        CreateBoard("BoardOil").Build();

        var service = CreateService();
        var result = await service.CreateColumnAsync(new CreateColumnRequest("   ", null));

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
        var result = await service.CreateColumnAsync(new CreateColumnRequest(longTitle, null));

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
        var result = await service.CreateColumnAsync(new CreateColumnRequest("bad@title", null));

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
        var result = await service.UpdateColumnAsync(999_999, new UpdateColumnRequest("X", null));

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Column not found.", result.Message);
    }

    [Fact]
    public async Task UpdateColumnAsync_WhenNoBoardExists_ShouldReturnInternalError()
    {
        var service = CreateService();
        var result = await service.UpdateColumnAsync(1, new UpdateColumnRequest("X", null));

        Assert.False(result.Success);
        Assert.Equal(500, result.StatusCode);
        Assert.Equal("No board exists. Bootstrap has not run.", result.Message);
    }

    [Fact]
    public async Task UpdateColumnAsync_WhenUpdatingTitleOnly_ShouldPersistTrimmedTitle()
    {
        var board = CreateBoard("BoardOil")
            .AddColumn("Old")
            .Build();
        var columnId = board.GetColumn("Old").Id;

        var service = CreateService();
        var result = await service.UpdateColumnAsync(columnId, new UpdateColumnRequest("  New Title  ", null));

        Assert.True(result.Success);
        Assert.Equal("New Title", result.Data!.Title);
        var stored = await DbContextForAssert.Columns.SingleAsync(x => x.Id == columnId);

        Assert.Equal("New Title", stored.Title);
        Assert.False(string.IsNullOrWhiteSpace(stored.SortKey));
    }

    [Fact]
    public async Task UpdateColumnAsync_WhenReordering_ShouldPersistOrder()
    {
        var board = CreateBoard("BoardOil")
            .AddColumn("A")
            .AddColumn("B")
            .AddColumn("C")
            .Build();
        var movingColumnId = board.GetColumn("C").Id;

        var service = CreateService();
        var result = await service.UpdateColumnAsync(movingColumnId, new UpdateColumnRequest(null, 0));

        Assert.True(result.Success);
        Assert.Equal(0, result.Data!.Position);
        var titles = await GetOrderedColumnTitlesAsync();

        Assert.Equal(["C", "A", "B"], titles);
    }

    [Fact]
    public async Task UpdateColumnAsync_WhenTitleInvalid_ShouldReturnValidationErrorForTitle()
    {
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var columnId = board.GetColumn("Todo").Id;

        var service = CreateService();
        var result = await service.UpdateColumnAsync(columnId, new UpdateColumnRequest("bad@title", null));

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
        var result = await service.DeleteColumnAsync(deletingId);

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
    }

    [Fact]
    public async Task DeleteColumnAsync_WhenColumnMissing_ShouldReturnOk()
    {
        CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();

        var service = CreateService();
        var result = await service.DeleteColumnAsync(999_999);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task DeleteColumnAsync_WhenNoBoardExists_ShouldReturnInternalError()
    {
        var service = CreateService();
        var result = await service.DeleteColumnAsync(1);

        Assert.False(result.Success);
        Assert.Equal(500, result.StatusCode);
        Assert.Equal("No board exists. Bootstrap has not run.", result.Message);
    }

    private async Task<List<string>> GetOrderedColumnTitlesAsync() =>
        await DbContextForAssert.Columns
            .OrderBy(x => x.SortKey)
            .Select(x => x.Title)
            .ToListAsync();

    private ColumnService CreateService()
    {
        var dbContext = CreateDbContextForAct();
        IBoardRepository boardRepository = new BoardRepository(dbContext);
        IColumnRepository columnRepository = new ColumnRepository(dbContext);
        IColumnValidator validator = new ColumnValidator();
        return new ColumnService(boardRepository, columnRepository, validator, new TestBoardEvents());
    }
}
