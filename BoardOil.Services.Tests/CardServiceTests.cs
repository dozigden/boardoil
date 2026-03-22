using BoardOil.Abstractions;
using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Card;
using BoardOil.Ef.Repositories;
using BoardOil.Services.Card;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using TagEntity = BoardOil.Persistence.Abstractions.Entities.EntityTag;

namespace BoardOil.Services.Tests;

public sealed class CardServiceTests : TestBaseDb
{
    [Fact]
    public async Task CreateCardAsync_WhenColumnEmpty_ShouldCreateCardAtPositionZero()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();
        var todoColumnId = board.GetColumn("Todo").Id;

        // Act
        var service = CreateService();

        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, "New Card", "Desc", null, null));

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
        Assert.Empty(result.Data.TagNames);
    }

    [Fact]
    public async Task CreateCardAsync_WhenTagsProvided_ShouldAssignTagsUsingExistingTagCatalogueEntries()
    {
        // Arrange
        await SeedTagsForArrangeAsync("Bug", "Needs Triage", "Sprint 1");
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();
        var todoColumnId = board.GetColumn("Todo").Id;

        // Act
        var service = CreateService();
        var result = await service.CreateCardAsync(new CreateCardRequest(
            BoardColumnId: todoColumnId,
            Title: "Tagged",
            Description: "Desc",
            Position: null,
            TagNames: ["Bug", "Needs Triage", "Bug", "Sprint 1"]));

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(["Bug", "Needs Triage", "Sprint 1"], result.Data!.TagNames);

        var storedTags = await DbContextForAssert.Tags
            .OrderBy(x => x.Name)
            .Select(x => x.Name)
            .ToListAsync();
        Assert.Equal(["Bug", "Needs Triage", "Sprint 1"], storedTags);

        var storedCardTags = await DbContextForAssert.CardTags
            .OrderBy(x => x.TagName)
            .Select(x => x.TagName)
            .ToListAsync();
        Assert.Equal(["Bug", "Needs Triage", "Sprint 1"], storedCardTags);
    }

    [Fact]
    public async Task CreateCardAsync_WhenPositionIsZero_ShouldInsertAtStart()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("A", "1")
            .AddCard("B", "2")
            .AddColumn("Doing")
            .Build();
        var todoColumnId = board.GetColumn("Todo").Id;

        // Act
        var service = CreateService();
        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, "Start", "X", 0, null));

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
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("A", "1")
            .AddCard("B", "2")
            .AddColumn("Doing")
            .Build();
        var todoColumnId = board.GetColumn("Todo").Id;

        // Act
        var service = CreateService();
        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, "Middle", "X", 1, null));

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
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("A", "1")
            .AddCard("B", "2")
            .AddColumn("Doing")
            .Build();
        var todoColumnId = board.GetColumn("Todo").Id;

        // Act
        var service = CreateService();
        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, "End", "X", null, null));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Position);
        var titles = await GetOrderedTitlesAsync(DbContextForAssert, todoColumnId);

        Assert.Equal(["A", "B", "End"], titles);
    }

    [Fact]
    public async Task CreateCardAsync_WhenColumnMissing_ShouldReturnValidationErrorForBoardColumnId()
    {
        // Arrange
        CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();

        // Act
        var service = CreateService();

        var result = await service.CreateCardAsync(new CreateCardRequest(999_999, "New", "Desc", null, null));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("boardColumnId"));
    }

    [Fact]
    public async Task UpdateCardAsync_WhenUpdatingTitleOnly_ShouldPersistTitle()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Old", "Desc")
            .AddColumn("Doing")
            .Build();
        var cardId = board.GetCard("Todo", "Old").Id;

        // Act
        var service = CreateService();
        var result = await service.UpdateCardAsync(cardId, new UpdateCardRequest("  New Title  ", null, null));

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
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Title", "Old")
            .AddColumn("Doing")
            .Build();
        var cardId = board.GetCard("Todo", "Title").Id;

        // Act
        var service = CreateService();
        var result = await service.UpdateCardAsync(cardId, new UpdateCardRequest(null, "New Description", null));

        // Assert
        Assert.True(result.Success);
        Assert.Equal("New Description", result.Data!.Description);
        var stored = await DbContextForAssert.Cards.SingleAsync(x => x.Id == cardId);

        Assert.Equal("Title", stored.Title);
        Assert.Equal("New Description", stored.Description);
    }

    [Fact]
    public async Task UpdateCardAsync_WhenTagNamesProvided_ShouldReplaceAssignedTags()
    {
        // Arrange
        await SeedTagsForArrangeAsync("Bug", "Urgent", "Ops");
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Title", "Old")
            .AddColumn("Doing")
            .Build();
        var cardId = board.GetCard("Todo", "Title").Id;

        var setupService = CreateService();
        var seedResult = await setupService.UpdateCardAsync(cardId, new UpdateCardRequest(
            Title: null,
            Description: null,
            TagNames: ["Bug", "Urgent"]));
        Assert.True(seedResult.Success);

        // Act
        var service = CreateService();
        var result = await service.UpdateCardAsync(cardId, new UpdateCardRequest(
            Title: null,
            Description: null,
            TagNames: ["Urgent", "Ops"]));

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(["Urgent", "Ops"], result.Data!.TagNames);

        var storedCardTags = await DbContextForAssert.CardTags
            .Where(x => x.CardId == cardId)
            .OrderBy(x => x.TagName)
            .Select(x => x.TagName)
            .ToListAsync();
        Assert.Equal(["Ops", "Urgent"], storedCardTags);
    }

    [Fact]
    public async Task MoveCardAsync_WhenReorderingWithinSameColumn_ShouldReorder()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("A", "1")
            .AddCard("B", "2")
            .AddCard("C", "3")
            .AddColumn("Doing")
            .Build();
        var todoColumnId = board.GetColumn("Todo").Id;
        var movingCardId = board.GetCard("Todo", "C").Id;

        // Act
        var service = CreateService();
        var result = await service.MoveCardAsync(movingCardId, new MoveCardRequest(todoColumnId, 0));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.Data!.Position);
        var titles = await GetOrderedTitlesAsync(DbContextForAssert, todoColumnId);

        Assert.Equal(["C", "A", "B"], titles);
    }

    [Fact]
    public async Task MoveCardAsync_WhenMovingCardToDifferentColumnWithOccupiedPosition_ShouldSucceed()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Move me", "source")
            .AddColumn("Doing")
            .AddCard("Existing", "target")
            .Build();
        var todoColumnId = board.GetColumn("Todo").Id;
        var doingColumnId = board.GetColumn("Doing").Id;
        var cardToMoveId = board.GetCard("Todo", "Move me").Id;

        // Act
        var service = CreateService();
        var result = await service.MoveCardAsync(
            cardToMoveId,
            new MoveCardRequest(
                BoardColumnId: doingColumnId,
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
    public async Task MoveCardAsync_WhenMovingCardToDifferentColumnWithNullPosition_ShouldAppendToEnd()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Move me", "source")
            .AddColumn("Doing")
            .AddCard("A", "1")
            .AddCard("B", "2")
            .Build();
        var doingColumnId = board.GetColumn("Doing").Id;
        var movingCardId = board.GetCard("Todo", "Move me").Id;

        // Act
        var service = CreateService();
        var result = await service.MoveCardAsync(
            movingCardId,
            new MoveCardRequest(
                BoardColumnId: doingColumnId,
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
        CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();

        // Act
        var service = CreateService();

        var result = await service.UpdateCardAsync(999_999, new UpdateCardRequest("X", null, null));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Card not found.", result.Message);
    }

    [Fact]
    public async Task MoveCardAsync_WhenTargetColumnMissing_ShouldReturnValidationErrorForBoardColumnId()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Card", "Desc")
            .AddColumn("Doing")
            .Build();
        var cardId = board.GetCard("Todo", "Card").Id;

        // Act
        var service = CreateService();
        var result = await service.MoveCardAsync(cardId, new MoveCardRequest(999_999, 0));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("boardColumnId"));
    }

    [Fact]
    public async Task DeleteCardAsync_WhenCardExists_ShouldRemoveCard()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Delete me", "Desc")
            .AddColumn("Doing")
            .Build();
        var cardId = board.GetCard("Todo", "Delete me").Id;

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
    public async Task DeleteCardAsync_WhenCardMissing_ShouldReturnOk()
    {
        // Arrange
        CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();

        // Act
        var service = CreateService();
        var result = await service.DeleteCardAsync(999_999);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task CreateCardAsync_WhenTitleHasInvalidCharacters_ShouldReturnValidationErrorForTitle()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();
        var todoColumnId = board.GetColumn("Todo").Id;

        // Act
        var service = CreateService();
        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, "bad@title", "Desc", null, null));

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
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();
        var todoColumnId = board.GetColumn("Todo").Id;

        var longTitle = new string('A', 201);

        // Act
        var service = CreateService();
        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, longTitle, "Desc", null, null));

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
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();
        var todoColumnId = board.GetColumn("Todo").Id;

        // Act
        var service = CreateService();
        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, "   ", "Desc", null, null));

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
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();
        var todoColumnId = board.GetColumn("Todo").Id;

        var longDescription = new string('D', 5001);

        // Act
        var service = CreateService();
        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, "ValidTitle", longDescription, null, null));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("description"));
    }

    [Fact]
    public async Task CreateCardAsync_WhenTagMissing_ShouldReturnValidationErrorForTagNames()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();
        var todoColumnId = board.GetColumn("Todo").Id;
        var missingTag = "MissingTag";

        // Act
        var service = CreateService();
        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, "ValidTitle", "Desc", null, [missingTag]));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("tagNames"));
    }

    [Fact]
    public async Task CreateCardAsync_WhenTagExistsWithComma_ShouldAssignTag()
    {
        // Arrange
        await SeedTagsForArrangeAsync("Bug,Urgent");
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();
        var todoColumnId = board.GetColumn("Todo").Id;

        // Act
        var service = CreateService();
        var result = await service.CreateCardAsync(new CreateCardRequest(todoColumnId, "ValidTitle", "Desc", null, ["Bug,Urgent"]));

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(["Bug,Urgent"], result.Data!.TagNames);
    }

    private CardService CreateService()
    {
        return ResolveService<CardService>();
    }

    private async Task SeedTagsForArrangeAsync(params string[] tagNames)
    {
        var now = DateTime.UtcNow;
        DbContextForArrange.Tags.AddRange(tagNames.Select(tagName => new TagEntity
        {
            Name = tagName,
            NormalisedName = tagName.ToUpperInvariant(),
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#224466","textColorMode":"auto"}""",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }));
        await DbContextForArrange.SaveChangesAsync();
    }
}
