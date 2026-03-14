using BoardOil.Ef;
using BoardOil.Services.Contracts;
using BoardOil.Services.Implementations;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardServiceTests
{
    [Fact]
    public async Task CreateCardAsync_WhenColumnEmpty_ShouldCreateCardAtPositionZero()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync(harness);

        await using var actDb = harness.CreateDbContext();
        var service = TestServiceFactory.CreateCardService(actDb);

        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, "New Card", "Desc", null));

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(0, result.Data!.Position);
        Assert.Equal(todoColumnId, result.Data.BoardColumnId);
        Assert.Equal("New Card", result.Data.Title);
        Assert.Equal("Desc", result.Data.Description);

        await using var assertDb = harness.CreateDbContext();
        var stored = await assertDb.Cards.SingleAsync();
        Assert.Equal(todoColumnId, stored.BoardColumnId);
        Assert.Equal("New Card", stored.Title);
        Assert.Equal("Desc", stored.Description);
    }

    [Fact]
    public async Task CreateCardAsync_WhenPositionIsZero_ShouldInsertAtStart()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync(harness);

        await using (var arrangeDb = harness.CreateDbContext())
        {
            await TestSeedData.SeedCardsAsync(
                arrangeDb,
                todoColumnId,
                ("A", "1", TestSeedData.OrderedSortKeys[1]),
                ("B", "2", TestSeedData.OrderedSortKeys[2]));
        }

        await using var actDb = harness.CreateDbContext();
        var service = TestServiceFactory.CreateCardService(actDb);
        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, "Start", "X", 0));

        Assert.True(result.Success);
        Assert.Equal(0, result.Data!.Position);

        await using var assertDb = harness.CreateDbContext();
        var titles = await GetOrderedTitlesAsync(assertDb, todoColumnId);
        Assert.Equal(["Start", "A", "B"], titles);
    }

    [Fact]
    public async Task CreateCardAsync_WhenPositionIsMiddle_ShouldInsertInMiddle()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync(harness);

        await using (var arrangeDb = harness.CreateDbContext())
        {
            await TestSeedData.SeedCardsAsync(
                arrangeDb,
                todoColumnId,
                ("A", "1", TestSeedData.OrderedSortKeys[0]),
                ("B", "2", TestSeedData.OrderedSortKeys[2]));
        }

        await using var actDb = harness.CreateDbContext();
        var service = TestServiceFactory.CreateCardService(actDb);
        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, "Middle", "X", 1));

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.Position);

        await using var assertDb = harness.CreateDbContext();
        var titles = await GetOrderedTitlesAsync(assertDb, todoColumnId);
        Assert.Equal(["A", "Middle", "B"], titles);
    }

    [Fact]
    public async Task CreateCardAsync_WhenPositionIsNull_ShouldAppendToEnd()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync(harness);

        await using (var arrangeDb = harness.CreateDbContext())
        {
            await TestSeedData.SeedCardsAsync(
                arrangeDb,
                todoColumnId,
                ("A", "1", TestSeedData.OrderedSortKeys[0]),
                ("B", "2", TestSeedData.OrderedSortKeys[1]));
        }

        await using var actDb = harness.CreateDbContext();
        var service = TestServiceFactory.CreateCardService(actDb);
        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, "End", "X", null));

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Position);

        await using var assertDb = harness.CreateDbContext();
        var titles = await GetOrderedTitlesAsync(assertDb, todoColumnId);
        Assert.Equal(["A", "B", "End"], titles);
    }

    [Fact]
    public async Task CreateCardAsync_WhenColumnMissing_ShouldReturnNotFound()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        await SeedTwoColumnBoardAsync(harness);

        await using var actDb = harness.CreateDbContext();
        var service = TestServiceFactory.CreateCardService(actDb);

        var result = await service.CreateCardAsync(new CreateCardRequest(999_999, "New", "Desc", null));

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Column not found.", result.Message);
    }

    [Fact]
    public async Task UpdateCardAsync_WhenUpdatingTitleOnly_ShouldPersistTitle()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync(harness);
        var cardId = await SeedSingleCardAsync(harness, todoColumnId, "Old", "Desc");

        await using var actDb = harness.CreateDbContext();
        var service = TestServiceFactory.CreateCardService(actDb);
        var result = await service.UpdateCardAsync(cardId, new UpdateCardRequest(null, "  New Title  ", null, null));

        Assert.True(result.Success);
        Assert.Equal("New Title", result.Data!.Title);

        await using var assertDb = harness.CreateDbContext();
        var stored = await assertDb.Cards.SingleAsync(x => x.Id == cardId);
        Assert.Equal("New Title", stored.Title);
        Assert.Equal("Desc", stored.Description);
    }

    [Fact]
    public async Task UpdateCardAsync_WhenUpdatingDescriptionOnly_ShouldPersistDescription()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync(harness);
        var cardId = await SeedSingleCardAsync(harness, todoColumnId, "Title", "Old");

        await using var actDb = harness.CreateDbContext();
        var service = TestServiceFactory.CreateCardService(actDb);
        var result = await service.UpdateCardAsync(cardId, new UpdateCardRequest(null, null, "New Description", null));

        Assert.True(result.Success);
        Assert.Equal("New Description", result.Data!.Description);

        await using var assertDb = harness.CreateDbContext();
        var stored = await assertDb.Cards.SingleAsync(x => x.Id == cardId);
        Assert.Equal("Title", stored.Title);
        Assert.Equal("New Description", stored.Description);
    }

    [Fact]
    public async Task UpdateCardAsync_WhenReorderingWithinSameColumn_ShouldReorder()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync(harness);
        int movingCardId;
        await using (var arrangeDb = harness.CreateDbContext())
        {
            var cards = await TestSeedData.SeedCardsAsync(
                arrangeDb,
                todoColumnId,
                ("A", "1", TestSeedData.OrderedSortKeys[0]),
                ("B", "2", TestSeedData.OrderedSortKeys[1]),
                ("C", "3", TestSeedData.OrderedSortKeys[2]));
            movingCardId = cards[2].Id;
        }

        await using var actDb = harness.CreateDbContext();
        var service = TestServiceFactory.CreateCardService(actDb);
        var result = await service.UpdateCardAsync(movingCardId, new UpdateCardRequest(null, null, null, 0));

        Assert.True(result.Success);
        Assert.Equal(0, result.Data!.Position);

        await using var assertDb = harness.CreateDbContext();
        var titles = await GetOrderedTitlesAsync(assertDb, todoColumnId);
        Assert.Equal(["C", "A", "B"], titles);
    }

    [Fact]
    public async Task UpdateCardAsync_WhenMovingCardToDifferentColumnWithOccupiedPosition_ShouldSucceed()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        var (todoColumnId, doingColumnId) = await SeedTwoColumnBoardAsync(harness);
        int cardToMoveId;
        await using (var arrangeDb = harness.CreateDbContext())
        {
            var sourceCards = await TestSeedData.SeedCardsAsync(
                arrangeDb,
                todoColumnId,
                ("Move me", "source", TestSeedData.OrderedSortKeys[2]));
            cardToMoveId = sourceCards[0].Id;

            await TestSeedData.SeedCardsAsync(
                arrangeDb,
                doingColumnId,
                ("Existing", "target", TestSeedData.OrderedSortKeys[2]));
        }

        await using var actDb = harness.CreateDbContext();
        var service = TestServiceFactory.CreateCardService(actDb);
        var result = await service.UpdateCardAsync(
            cardToMoveId,
            new UpdateCardRequest(
                BoardColumnId: doingColumnId,
                Title: null,
                Description: null,
                Position: 1));

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(doingColumnId, result.Data!.BoardColumnId);
        Assert.Equal(1, result.Data.Position);

        await using var assertDb = harness.CreateDbContext();
        var todoTitles = await GetOrderedTitlesAsync(assertDb, todoColumnId);
        var doingTitles = await GetOrderedTitlesAsync(assertDb, doingColumnId);
        Assert.Empty(todoTitles);
        Assert.Equal(["Existing", "Move me"], doingTitles);
    }

    [Fact]
    public async Task UpdateCardAsync_WhenMovingCardToDifferentColumnWithNullPosition_ShouldAppendToEnd()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        var (todoColumnId, doingColumnId) = await SeedTwoColumnBoardAsync(harness);
        int movingCardId;
        await using (var arrangeDb = harness.CreateDbContext())
        {
            var sourceCards = await TestSeedData.SeedCardsAsync(
                arrangeDb,
                todoColumnId,
                ("Move me", "source", TestSeedData.OrderedSortKeys[0]));
            movingCardId = sourceCards[0].Id;

            await TestSeedData.SeedCardsAsync(
                arrangeDb,
                doingColumnId,
                ("A", "1", TestSeedData.OrderedSortKeys[0]),
                ("B", "2", TestSeedData.OrderedSortKeys[1]));
        }

        await using var actDb = harness.CreateDbContext();
        var service = TestServiceFactory.CreateCardService(actDb);
        var result = await service.UpdateCardAsync(
            movingCardId,
            new UpdateCardRequest(
                BoardColumnId: doingColumnId,
                Title: null,
                Description: null,
                Position: null));

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Position);

        await using var assertDb = harness.CreateDbContext();
        var doingTitles = await GetOrderedTitlesAsync(assertDb, doingColumnId);
        Assert.Equal(["A", "B", "Move me"], doingTitles);
    }

    [Fact]
    public async Task UpdateCardAsync_WhenCardMissing_ShouldReturnNotFound()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        await SeedTwoColumnBoardAsync(harness);

        await using var actDb = harness.CreateDbContext();
        var service = TestServiceFactory.CreateCardService(actDb);

        var result = await service.UpdateCardAsync(999_999, new UpdateCardRequest(null, "X", null, null));

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Card not found.", result.Message);
    }

    [Fact]
    public async Task UpdateCardAsync_WhenTargetColumnMissing_ShouldReturnNotFound()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync(harness);
        var cardId = await SeedSingleCardAsync(harness, todoColumnId, "Card", "Desc");

        await using var actDb = harness.CreateDbContext();
        var service = TestServiceFactory.CreateCardService(actDb);
        var result = await service.UpdateCardAsync(cardId, new UpdateCardRequest(999_999, null, null, 0));

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Column not found.", result.Message);
    }

    [Fact]
    public async Task DeleteCardAsync_WhenCardExists_ShouldRemoveCard()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync(harness);
        var cardId = await SeedSingleCardAsync(harness, todoColumnId, "Delete me", "Desc");

        await using var actDb = harness.CreateDbContext();
        var service = TestServiceFactory.CreateCardService(actDb);
        var result = await service.DeleteCardAsync(cardId);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);

        await using var assertDb = harness.CreateDbContext();
        var exists = await assertDb.Cards.AnyAsync(x => x.Id == cardId);
        Assert.False(exists);
    }

    [Fact]
    public async Task DeleteCardAsync_WhenCardMissing_ShouldReturnNotFound()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        await SeedTwoColumnBoardAsync(harness);

        await using var actDb = harness.CreateDbContext();
        var service = TestServiceFactory.CreateCardService(actDb);
        var result = await service.DeleteCardAsync(999_999);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Card not found.", result.Message);
    }

    [Fact]
    public async Task CreateCardAsync_WhenTitleHasInvalidCharacters_ShouldReturnValidationErrorForTitle()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync(harness);

        await using var actDb = harness.CreateDbContext();
        var service = TestServiceFactory.CreateCardService(actDb);
        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, "bad@title", "Desc", null));

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("title"));
    }

    [Fact]
    public async Task CreateCardAsync_WhenTitleTooLong_ShouldReturnValidationErrorForTitle()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync(harness);

        var longTitle = new string('A', 201);
        await using var actDb = harness.CreateDbContext();
        var service = TestServiceFactory.CreateCardService(actDb);
        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, longTitle, "Desc", null));

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("title"));
    }

    [Fact]
    public async Task CreateCardAsync_WhenTitleIsWhitespace_ShouldReturnValidationErrorForTitle()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync(harness);

        await using var actDb = harness.CreateDbContext();
        var service = TestServiceFactory.CreateCardService(actDb);
        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, "   ", "Desc", null));

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("title"));
    }

    [Fact]
    public async Task CreateCardAsync_WhenDescriptionTooLong_ShouldReturnValidationErrorForDescription()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync(harness);

        var longDescription = new string('D', 5001);
        await using var actDb = harness.CreateDbContext();
        var service = TestServiceFactory.CreateCardService(actDb);
        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, "ValidTitle", longDescription, null));

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("description"));
    }

    private static async Task<(int TodoColumnId, int DoingColumnId)> SeedTwoColumnBoardAsync(SqliteTestHarness harness)
    {
        await using var db = harness.CreateDbContext();
        var board = await TestSeedData.SeedDefaultBoardAsync(db);
        var columns = await TestSeedData.SeedColumnsAsync(db, board.Id, "Todo", "Doing");
        return (columns[0].Id, columns[1].Id);
    }

    private static async Task<int> SeedSingleCardAsync(SqliteTestHarness harness, int columnId, string title, string description)
    {
        await using var db = harness.CreateDbContext();
        var cards = await TestSeedData.SeedCardsAsync(
            db,
            columnId,
            (title, description, TestSeedData.OrderedSortKeys[2]));
        return cards[0].Id;
    }

    private static async Task<List<string>> GetOrderedTitlesAsync(BoardOilDbContext db, int columnId) =>
        await db.Cards
            .Where(x => x.BoardColumnId == columnId)
            .OrderBy(x => x.SortKey)
            .Select(x => x.Title)
            .ToListAsync();
}
