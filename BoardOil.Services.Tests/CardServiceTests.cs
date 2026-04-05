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
    public async Task CreateCardAsync_WhenColumnEmpty_ShouldCreateCardWithSortKey()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();
        var todoColumnId = board.GetColumn("Todo").Id;

        // Act
        var service = CreateService();

        var result = await service.CreateCardAsync(1, new CreateCardRequest(todoColumnId, "New Card", "Desc", null), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.False(string.IsNullOrWhiteSpace(result.Data!.SortKey));
        Assert.Equal(todoColumnId, result.Data.BoardColumnId);
        Assert.Equal("New Card", result.Data.Title);
        Assert.Equal("Desc", result.Data.Description);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        var systemCardType = await DbContextForAssert.CardTypes.SingleAsync();

        Assert.Equal(todoColumnId, stored.BoardColumnId);
        Assert.Equal(systemCardType.Id, stored.CardTypeId);
        Assert.Equal("New Card", stored.Title);
        Assert.Equal("Desc", stored.Description);
        Assert.True(systemCardType.IsSystem);
        Assert.Equal("Story", systemCardType.Name);
        Assert.Empty(result.Data.TagNames);
    }

    [Fact]
    public async Task CreateCardAsync_WhenTagsProvided_ShouldAssignTagsUsingExistingTagCatalogueEntries()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();
        await SeedTagsForArrangeAsync(board.BoardId, "Bug", "Needs Triage", "Sprint 1");
        var todoColumnId = board.GetColumn("Todo").Id;

        // Act
        var service = CreateService();
        var result = await service.CreateCardAsync(1, new CreateCardRequest(
            BoardColumnId: todoColumnId,
            Title: "Tagged",
            Description: "Desc",
            TagNames: ["Bug", "Needs Triage", "Bug", "Sprint 1"]), ActorUserId);

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
            .Include(x => x.Tag)
            .OrderBy(x => x.Tag.Name)
            .Select(x => x.Tag.Name)
            .ToListAsync();
        Assert.Equal(["Bug", "Needs Triage", "Sprint 1"], storedCardTags);
    }

    [Fact]
    public async Task CreateCardAsync_WhenColumnHasCards_ShouldAppendToEnd()
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
        var result = await service.CreateCardAsync(1, new CreateCardRequest(todoColumnId, "End", "X", null), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Data!.SortKey));
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

        var result = await service.CreateCardAsync(1, new CreateCardRequest(999_999, "New", "Desc", null), ActorUserId);

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
        var systemCardTypeId = await GetSystemCardTypeIdForBoardAsync(board.BoardId);

        // Act
        var service = CreateService();
        var result = await service.UpdateCardAsync(1, cardId, new UpdateCardRequest("  New Title  ", "Desc", [], systemCardTypeId), ActorUserId);

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
        var systemCardTypeId = await GetSystemCardTypeIdForBoardAsync(board.BoardId);

        // Act
        var service = CreateService();
        var result = await service.UpdateCardAsync(1, cardId, new UpdateCardRequest("Title", "New Description", [], systemCardTypeId), ActorUserId);

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
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Title", "Old")
            .AddColumn("Doing")
            .Build();
        await SeedTagsForArrangeAsync(board.BoardId, "Bug", "Urgent", "Ops");
        var cardId = board.GetCard("Todo", "Title").Id;
        var systemCardTypeId = await GetSystemCardTypeIdForBoardAsync(board.BoardId);

        var setupService = CreateService();
        var seedResult = await setupService.UpdateCardAsync(1, cardId, new UpdateCardRequest(
            Title: "Title",
            Description: "Old",
            TagNames: ["Bug", "Urgent"],
            CardTypeId: systemCardTypeId), ActorUserId);
        Assert.True(seedResult.Success);

        // Act
        var service = CreateService();
        var result = await service.UpdateCardAsync(1, cardId, new UpdateCardRequest(
            Title: "Title",
            Description: "Old",
            TagNames: ["Urgent", "Ops"],
            CardTypeId: systemCardTypeId), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(["Ops", "Urgent"], result.Data!.TagNames);

        var storedCardTags = await DbContextForAssert.CardTags
            .Where(x => x.CardId == cardId)
            .Include(x => x.Tag)
            .OrderBy(x => x.Tag.Name)
            .Select(x => x.Tag.Name)
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
        var result = await service.MoveCardAsync(1, movingCardId, new MoveCardRequest(todoColumnId, null), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Data!.SortKey));
        var titles = await GetOrderedTitlesAsync(DbContextForAssert, todoColumnId);

        Assert.Equal(["C", "A", "B"], titles);
    }

    [Fact]
    public async Task MoveCardAsync_WhenMovingCardToDifferentColumnAfterAnchor_ShouldSucceed()
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
        var existingCardId = board.GetCard("Doing", "Existing").Id;

        // Act
        var service = CreateService();
        var result = await service.MoveCardAsync(1, 
            cardToMoveId,
            new MoveCardRequest(
                BoardColumnId: doingColumnId,
                PositionAfterCardId: existingCardId), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(doingColumnId, result.Data!.BoardColumnId);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.SortKey));
        var todoTitles = await GetOrderedTitlesAsync(DbContextForAssert, todoColumnId);
        var doingTitles = await GetOrderedTitlesAsync(DbContextForAssert, doingColumnId);

        Assert.Empty(todoTitles);
        Assert.Equal(["Existing", "Move me"], doingTitles);
    }

    [Fact]
    public async Task MoveCardAsync_WhenMovingCardToDifferentColumnWithNullPositionAfterCardId_ShouldMoveToStart()
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
        var result = await service.MoveCardAsync(1, 
            movingCardId,
            new MoveCardRequest(
                BoardColumnId: doingColumnId,
                PositionAfterCardId: null), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Data!.SortKey));
        var doingTitles = await GetOrderedTitlesAsync(DbContextForAssert, doingColumnId);

        Assert.Equal(["Move me", "A", "B"], doingTitles);
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

        var result = await service.UpdateCardAsync(1, 999_999, new UpdateCardRequest("X", string.Empty, [], 1), ActorUserId);

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
        var result = await service.MoveCardAsync(1, cardId, new MoveCardRequest(999_999, null), ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("boardColumnId"));
    }

    [Fact]
    public async Task MoveCardAsync_WhenPositionAfterCardIdMatchesMovingCard_ShouldReturnValidationError()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Card", "Desc")
            .Build();
        var cardId = board.GetCard("Todo", "Card").Id;
        var todoColumnId = board.GetColumn("Todo").Id;

        // Act
        var service = CreateService();
        var result = await service.MoveCardAsync(1, 
            cardId,
            new MoveCardRequest(
                BoardColumnId: todoColumnId,
                PositionAfterCardId: cardId), ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("positionAfterCardId"));
    }

    [Fact]
    public async Task MoveCardAsync_WhenPositionAfterCardIdNotInTargetColumn_ShouldReturnValidationError()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Move me", "Desc")
            .AddColumn("Doing")
            .AddCard("Target", "Desc")
            .AddColumn("Done")
            .AddCard("Foreign", "Desc")
            .Build();
        var movingCardId = board.GetCard("Todo", "Move me").Id;
        var doingColumnId = board.GetColumn("Doing").Id;
        var foreignCardId = board.GetCard("Done", "Foreign").Id;

        // Act
        var service = CreateService();
        var result = await service.MoveCardAsync(1, 
            movingCardId,
            new MoveCardRequest(
                BoardColumnId: doingColumnId,
                PositionAfterCardId: foreignCardId), ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("positionAfterCardId"));
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
        var result = await service.DeleteCardAsync(1, cardId, ActorUserId);

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
        var result = await service.DeleteCardAsync(1, 999_999, ActorUserId);

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
        var result = await service.CreateCardAsync(1, new CreateCardRequest(todoColumnId, "bad@title", "Desc", null), ActorUserId);

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
        var result = await service.CreateCardAsync(1, new CreateCardRequest(todoColumnId, longTitle, "Desc", null), ActorUserId);

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
        var result = await service.CreateCardAsync(1, new CreateCardRequest(todoColumnId, "   ", "Desc", null), ActorUserId);

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
        var result = await service.CreateCardAsync(1, new CreateCardRequest(todoColumnId, "ValidTitle", longDescription, null), ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("description"));
    }

    [Fact]
    public async Task CreateCardAsync_WhenTagMissing_ShouldAutoCreateTagAndAssignIt()
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
        var result = await service.CreateCardAsync(1, new CreateCardRequest(todoColumnId, "ValidTitle", "Desc", [missingTag]), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal([missingTag], result.Data!.TagNames);

        var storedTagNames = await DbContextForAssert.Tags
            .OrderBy(x => x.Name)
            .Select(x => x.Name)
            .ToListAsync();
        Assert.Equal([missingTag], storedTagNames);
    }

    [Fact]
    public async Task UpdateCardAsync_WhenTagMissing_ShouldAutoCreateTagAndAssignIt()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Title", "Old")
            .AddColumn("Doing")
            .Build();
        var cardId = board.GetCard("Todo", "Title").Id;
        var missingTag = "AutoTag";
        var systemCardTypeId = await GetSystemCardTypeIdForBoardAsync(board.BoardId);

        // Act
        var service = CreateService();
        var result = await service.UpdateCardAsync(1, cardId, new UpdateCardRequest(
            Title: "Title",
            Description: "Old",
            TagNames: [missingTag],
            CardTypeId: systemCardTypeId), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal([missingTag], result.Data!.TagNames);

        var storedTagNames = await DbContextForAssert.Tags
            .OrderBy(x => x.Name)
            .Select(x => x.Name)
            .ToListAsync();
        Assert.Equal([missingTag], storedTagNames);
    }

    [Fact]
    public async Task CreateCardAsync_WhenTagNameTooLong_ShouldReturnValidationErrorForTagNames()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();
        var todoColumnId = board.GetColumn("Todo").Id;
        var tooLongTag = new string('T', 41);

        // Act
        var service = CreateService();
        var result = await service.CreateCardAsync(1, new CreateCardRequest(todoColumnId, "ValidTitle", "Desc", [tooLongTag]), ActorUserId);

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
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();
        await SeedTagsForArrangeAsync(board.BoardId, "Bug,Urgent");
        var todoColumnId = board.GetColumn("Todo").Id;

        // Act
        var service = CreateService();
        var result = await service.CreateCardAsync(1, new CreateCardRequest(todoColumnId, "ValidTitle", "Desc", ["Bug,Urgent"]), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(["Bug,Urgent"], result.Data!.TagNames);
    }

    private CardService CreateService()
    {
        return ResolveService<CardService>();
    }

    private async Task SeedTagsForArrangeAsync(int boardId, params string[] tagNames)
    {
        var now = DateTime.UtcNow;
        DbContextForArrange.Tags.AddRange(tagNames.Select(tagName => new TagEntity
        {
            BoardId = boardId,
            Name = tagName,
            NormalisedName = tagName.ToUpperInvariant(),
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#224466","textColorMode":"auto"}""",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }));
        await DbContextForArrange.SaveChangesAsync();
    }

    private Task<int> GetSystemCardTypeIdForBoardAsync(int boardId) =>
        DbContextForArrange.CardTypes
            .Where(x => x.BoardId == boardId && x.IsSystem)
            .Select(x => x.Id)
            .SingleAsync();
}
