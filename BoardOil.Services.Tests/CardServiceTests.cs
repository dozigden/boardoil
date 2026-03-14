using BoardOil.Services.Contracts;
using BoardOil.Services.Implementations;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardServiceTests : TestBaseDb
{
    [Fact]
    public async Task CreateCardAsync_WhenColumnEmpty_ShouldCreateCardAtPositionZero()
    {
        // Arrange
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync();

        // Act
        var service = CreateService();

        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, "New Card", "Desc", null));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(0, result.Data!.Position);
        Assert.Equal(todoColumnId, result.Data.BoardColumnId);
        Assert.Equal("New Card", result.Data.Title);
        Assert.Equal("Desc", result.Data.Description);
        var stored = await DbContextForAssert.Cards.SingleAsync();

        Assert.Equal(todoColumnId, stored.BoardColumnId);
        Assert.Equal("New Card", stored.Title);
        Assert.Equal("Desc", stored.Description);
    }

    [Fact]
    public async Task CreateCardAsync_WhenPositionIsZero_ShouldInsertAtStart()
    {
        // Arrange
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync();

        await SeedCardsAsync(todoColumnId,
            ("A", "1", TestSeedData.OrderedSortKeys[1]),
            ("B", "2", TestSeedData.OrderedSortKeys[2]));

        // Act
        var service = CreateService();
        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, "Start", "X", 0));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.Data!.Position);
        var titles = await GetOrderedTitlesAsync(DbContextForAssert, todoColumnId);

        Assert.Equal(["Start", "A", "B"], titles);
    }

    [Fact]
    public async Task CreateCardAsync_WhenPositionIsMiddle_ShouldInsertInMiddle()
    {
        // Arrange
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync();

        await SeedCardsAsync(todoColumnId,
            ("A", "1", TestSeedData.OrderedSortKeys[0]),
            ("B", "2", TestSeedData.OrderedSortKeys[2]));

        // Act
        var service = CreateService();
        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, "Middle", "X", 1));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.Position);
        var titles = await GetOrderedTitlesAsync(DbContextForAssert, todoColumnId);

        Assert.Equal(["A", "Middle", "B"], titles);
    }

    [Fact]
    public async Task CreateCardAsync_WhenPositionIsNull_ShouldAppendToEnd()
    {
        // Arrange
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync();

        await SeedCardsAsync(todoColumnId,
            ("A", "1", TestSeedData.OrderedSortKeys[0]),
            ("B", "2", TestSeedData.OrderedSortKeys[1]));

        // Act
        var service = CreateService();
        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, "End", "X", null));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Position);
        var titles = await GetOrderedTitlesAsync(DbContextForAssert, todoColumnId);

        Assert.Equal(["A", "B", "End"], titles);
    }

    [Fact]
    public async Task CreateCardAsync_WhenColumnMissing_ShouldReturnNotFound()
    {
        // Arrange
        await SeedTwoColumnBoardAsync();

        // Act
        var service = CreateService();

        var result = await service.CreateCardAsync(new CreateCardRequest(999_999, "New", "Desc", null));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Column not found.", result.Message);
    }

    [Fact]
    public async Task UpdateCardAsync_WhenUpdatingTitleOnly_ShouldPersistTitle()
    {
        // Arrange
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync();
        var cardId = await SeedSingleCardAsync(todoColumnId, "Old", "Desc");

        // Act
        var service = CreateService();
        var result = await service.UpdateCardAsync(cardId, new UpdateCardRequest(null, "  New Title  ", null, null));

        // Assert
        Assert.True(result.Success);
        Assert.Equal("New Title", result.Data!.Title);
        var stored = await DbContextForAssert.Cards.SingleAsync(x => x.Id == cardId);

        Assert.Equal("New Title", stored.Title);
        Assert.Equal("Desc", stored.Description);
    }

    [Fact]
    public async Task UpdateCardAsync_WhenUpdatingDescriptionOnly_ShouldPersistDescription()
    {
        // Arrange
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync();
        var cardId = await SeedSingleCardAsync(todoColumnId, "Title", "Old");

        // Act
        var service = CreateService();
        var result = await service.UpdateCardAsync(cardId, new UpdateCardRequest(null, null, "New Description", null));

        // Assert
        Assert.True(result.Success);
        Assert.Equal("New Description", result.Data!.Description);
        var stored = await DbContextForAssert.Cards.SingleAsync(x => x.Id == cardId);

        Assert.Equal("Title", stored.Title);
        Assert.Equal("New Description", stored.Description);
    }

    [Fact]
    public async Task UpdateCardAsync_WhenReorderingWithinSameColumn_ShouldReorder()
    {
        // Arrange
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync();
        int movingCardId;
        {
            var cards = await SeedCardsAsync(todoColumnId,
                ("A", "1", TestSeedData.OrderedSortKeys[0]),
                ("B", "2", TestSeedData.OrderedSortKeys[1]),
                ("C", "3", TestSeedData.OrderedSortKeys[2]));
            movingCardId = cards[2].Id;
        }

        // Act
        var service = CreateService();
        var result = await service.UpdateCardAsync(movingCardId, new UpdateCardRequest(null, null, null, 0));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.Data!.Position);
        var titles = await GetOrderedTitlesAsync(DbContextForAssert, todoColumnId);

        Assert.Equal(["C", "A", "B"], titles);
    }

    [Fact]
    public async Task UpdateCardAsync_WhenMovingCardToDifferentColumnWithOccupiedPosition_ShouldSucceed()
    {
        // Arrange
        var (todoColumnId, doingColumnId) = await SeedTwoColumnBoardAsync();
        int cardToMoveId;
        {
            var sourceCards = await SeedCardsAsync(todoColumnId,
                ("Move me", "source", TestSeedData.OrderedSortKeys[2]));
            cardToMoveId = sourceCards[0].Id;

            await SeedCardsAsync(doingColumnId,
                ("Existing", "target", TestSeedData.OrderedSortKeys[2]));
        }

        // Act
        var service = CreateService();
        var result = await service.UpdateCardAsync(
            cardToMoveId,
            new UpdateCardRequest(
                BoardColumnId: doingColumnId,
                Title: null,
                Description: null,
                Position: 1));

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(doingColumnId, result.Data!.BoardColumnId);
        Assert.Equal(1, result.Data.Position);
        var todoTitles = await GetOrderedTitlesAsync(DbContextForAssert, todoColumnId);
        var doingTitles = await GetOrderedTitlesAsync(DbContextForAssert, doingColumnId);

        Assert.Empty(todoTitles);
        Assert.Equal(["Existing", "Move me"], doingTitles);
    }

    [Fact]
    public async Task UpdateCardAsync_WhenMovingCardToDifferentColumnWithNullPosition_ShouldAppendToEnd()
    {
        // Arrange
        var (todoColumnId, doingColumnId) = await SeedTwoColumnBoardAsync();
        int movingCardId;
        {
            var sourceCards = await SeedCardsAsync(todoColumnId,
                ("Move me", "source", TestSeedData.OrderedSortKeys[0]));
            movingCardId = sourceCards[0].Id;

            await SeedCardsAsync(doingColumnId,
                ("A", "1", TestSeedData.OrderedSortKeys[0]),
                ("B", "2", TestSeedData.OrderedSortKeys[1]));
        }

        // Act
        var service = CreateService();
        var result = await service.UpdateCardAsync(
            movingCardId,
            new UpdateCardRequest(
                BoardColumnId: doingColumnId,
                Title: null,
                Description: null,
                Position: null));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Position);
        var doingTitles = await GetOrderedTitlesAsync(DbContextForAssert, doingColumnId);

        Assert.Equal(["A", "B", "Move me"], doingTitles);
    }

    [Fact]
    public async Task UpdateCardAsync_WhenCardMissing_ShouldReturnNotFound()
    {
        // Arrange
        await SeedTwoColumnBoardAsync();

        // Act
        var service = CreateService();

        var result = await service.UpdateCardAsync(999_999, new UpdateCardRequest(null, "X", null, null));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Card not found.", result.Message);
    }

    [Fact]
    public async Task UpdateCardAsync_WhenTargetColumnMissing_ShouldReturnNotFound()
    {
        // Arrange
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync();
        var cardId = await SeedSingleCardAsync(todoColumnId, "Card", "Desc");

        // Act
        var service = CreateService();
        var result = await service.UpdateCardAsync(cardId, new UpdateCardRequest(999_999, null, null, 0));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Column not found.", result.Message);
    }

    [Fact]
    public async Task DeleteCardAsync_WhenCardExists_ShouldRemoveCard()
    {
        // Arrange
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync();
        var cardId = await SeedSingleCardAsync(todoColumnId, "Delete me", "Desc");

        // Act
        var service = CreateService();
        var result = await service.DeleteCardAsync(cardId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        var exists = await DbContextForAssert.Cards.AnyAsync(x => x.Id == cardId);

        Assert.False(exists);
    }

    [Fact]
    public async Task DeleteCardAsync_WhenCardMissing_ShouldReturnNotFound()
    {
        // Arrange
        await SeedTwoColumnBoardAsync();

        // Act
        var service = CreateService();
        var result = await service.DeleteCardAsync(999_999);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Card not found.", result.Message);
    }

    [Fact]
    public async Task CreateCardAsync_WhenTitleHasInvalidCharacters_ShouldReturnValidationErrorForTitle()
    {
        // Arrange
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync();

        // Act
        var service = CreateService();
        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, "bad@title", "Desc", null));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("title"));
    }

    [Fact]
    public async Task CreateCardAsync_WhenTitleTooLong_ShouldReturnValidationErrorForTitle()
    {
        // Arrange
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync();

        var longTitle = new string('A', 201);

        // Act
        var service = CreateService();
        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, longTitle, "Desc", null));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("title"));
    }

    [Fact]
    public async Task CreateCardAsync_WhenTitleIsWhitespace_ShouldReturnValidationErrorForTitle()
    {
        // Arrange
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync();

        // Act
        var service = CreateService();
        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, "   ", "Desc", null));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("title"));
    }

    [Fact]
    public async Task CreateCardAsync_WhenDescriptionTooLong_ShouldReturnValidationErrorForDescription()
    {
        // Arrange
        var (todoColumnId, _) = await SeedTwoColumnBoardAsync();

        var longDescription = new string('D', 5001);

        // Act
        var service = CreateService();
        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, "ValidTitle", longDescription, null));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("description"));
    }

    private CardService CreateService()
    {
        var dbContext = CreateDbContextForAct();
        return TestServiceFactory.CreateCardService(dbContext);
    }
}
