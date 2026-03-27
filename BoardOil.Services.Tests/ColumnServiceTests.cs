using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Column;
using BoardOil.Contracts.Column;
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
        var result = await service.GetColumnsAsync(1);

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
        var result = await service.GetColumnsAsync(1);

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
        var result = await service.CreateColumnAsync(1, new CreateColumnRequest("Done"));

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
        var result = await service.CreateColumnAsync(1, new CreateColumnRequest("Todo"));

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Board not found.", result.Message);
    }

    [Fact]
    public async Task CreateColumnAsync_WhenTitleIsWhitespace_ShouldReturnValidationErrorForTitle()
    {
        CreateBoard("BoardOil").Build();

        var service = CreateService();
        var result = await service.CreateColumnAsync(1, new CreateColumnRequest("   "));

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
        var result = await service.CreateColumnAsync(1, new CreateColumnRequest(longTitle));

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
        var result = await service.CreateColumnAsync(1, new CreateColumnRequest("bad@title"));

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
        var result = await service.UpdateColumnAsync(1, 999_999, new UpdateColumnRequest("X"));

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Column not found.", result.Message);
    }

    [Fact]
    public async Task UpdateColumnAsync_WhenNoBoardExists_ShouldReturnNotFound()
    {
        var service = CreateService();
        var result = await service.UpdateColumnAsync(1, 1, new UpdateColumnRequest("X"));

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
        var result = await service.UpdateColumnAsync(1, columnId, new UpdateColumnRequest("  New Title  "));

        Assert.True(result.Success);
        Assert.Equal("New Title", result.Data!.Title);
        var stored = await DbContextForAssert.Columns.SingleAsync(x => x.Id == columnId);

        Assert.Equal("New Title", stored.Title);
        Assert.False(string.IsNullOrWhiteSpace(stored.SortKey));
    }

    [Fact]
    public async Task MoveColumnAsync_WhenPositionAfterColumnIdIsNull_ShouldMoveToStart()
    {
        var board = CreateBoard("BoardOil")
            .AddColumn("A")
            .AddColumn("B")
            .AddColumn("C")
            .Build();
        var movingColumnId = board.GetColumn("C").Id;

        var service = CreateService();
        var result = await service.MoveColumnAsync(1, movingColumnId, new MoveColumnRequest(null));

        Assert.True(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Data!.SortKey));
        var titles = await GetOrderedColumnTitlesAsync();

        Assert.Equal(["C", "A", "B"], titles);
    }

    [Fact]
    public async Task MoveColumnAsync_WhenMovingAfterAnchor_ShouldPersistOrder()
    {
        var board = CreateBoard("BoardOil")
            .AddColumn("A")
            .AddColumn("B")
            .AddColumn("C")
            .Build();
        var movingColumnId = board.GetColumn("A").Id;
        var anchorColumnId = board.GetColumn("C").Id;

        var service = CreateService();
        var result = await service.MoveColumnAsync(1, 
            movingColumnId,
            new MoveColumnRequest(anchorColumnId));

        Assert.True(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Data!.SortKey));
        var titles = await GetOrderedColumnTitlesAsync();
        Assert.Equal(["B", "C", "A"], titles);
    }

    [Fact]
    public async Task MoveColumnAsync_WhenPositionAfterColumnIdMatchesMovingColumn_ShouldReturnValidationError()
    {
        var board = CreateBoard("BoardOil")
            .AddColumn("A")
            .Build();
        var movingColumnId = board.GetColumn("A").Id;

        var service = CreateService();
        var result = await service.MoveColumnAsync(1, 
            movingColumnId,
            new MoveColumnRequest(movingColumnId));

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("positionAfterColumnId"));
    }

    [Fact]
    public async Task MoveColumnAsync_WhenPositionAfterColumnIdDoesNotExist_ShouldReturnValidationError()
    {
        var board = CreateBoard("BoardOil")
            .AddColumn("A")
            .AddColumn("B")
            .Build();
        var movingColumnId = board.GetColumn("A").Id;

        var service = CreateService();
        var result = await service.MoveColumnAsync(1, 
            movingColumnId,
            new MoveColumnRequest(999_999));

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("positionAfterColumnId"));
    }

    [Fact]
    public async Task UpdateColumnAsync_WhenTitleInvalid_ShouldReturnValidationErrorForTitle()
    {
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var columnId = board.GetColumn("Todo").Id;

        var service = CreateService();
        var result = await service.UpdateColumnAsync(1, columnId, new UpdateColumnRequest("bad@title"));

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
        var result = await service.DeleteColumnAsync(1, deletingId);

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
        var result = await service.DeleteColumnAsync(1, 999_999);

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
        var result = await service.DeleteColumnAsync(boardOne.BoardId, boardTwoColumnId);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Column not found.", result.Message);
    }

    [Fact]
    public async Task DeleteColumnAsync_WhenNoBoardExists_ShouldReturnNotFound()
    {
        var service = CreateService();
        var result = await service.DeleteColumnAsync(1, 1);

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
