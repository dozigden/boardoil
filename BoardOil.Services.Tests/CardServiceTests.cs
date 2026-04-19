using BoardOil.Abstractions;
using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Card;
using BoardOil.Ef.Repositories;
using BoardOil.Services.Card;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using ArchivedCardEntity = BoardOil.Persistence.Abstractions.Entities.EntityArchivedCard;
using TagEntity = BoardOil.Persistence.Abstractions.Entities.EntityTag;

namespace BoardOil.Services.Tests;

public sealed class CardServiceTests : TestBaseDb
{
    private const int MaxCardDescriptionLength = 20_000;

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
        Assert.Empty(result.Data.Tags);
        Assert.Empty(result.Data.TagNames);
    }

    [Fact]
    public async Task CreateCardAsync_WhenColumnOmitted_ShouldCreateCardInLeftMostColumn()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();
        var leftMostColumnId = board.GetColumn("Todo").Id;

        // Act
        var service = CreateService();
        var result = await service.CreateCardAsync(1, new CreateCardRequest(null, "New Card", "Desc", null), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(leftMostColumnId, result.Data!.BoardColumnId);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(leftMostColumnId, stored.BoardColumnId);
    }

    [Fact]
    public async Task CreateCardAsync_WhenDescriptionIsNull_ShouldPersistEmptyString()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var todoColumnId = board.GetColumn("Todo").Id;

        // Act
        var service = CreateService();
        var result = await service.CreateCardAsync(1, new CreateCardRequest(todoColumnId, "New Card", null, null), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(string.Empty, result.Data!.Description);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(string.Empty, stored.Description);
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
        Assert.Equal(["Bug", "Needs Triage", "Sprint 1"], result.Data!.Tags.Select(x => x.Name).ToArray());
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
    public async Task CreateCardAsync_WhenColumnHasCards_ShouldInsertAtTop()
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

        Assert.Equal(["End", "A", "B"], titles);
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
        Assert.Equal(["Ops", "Urgent"], result.Data!.Tags.Select(x => x.Name).ToArray());
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
    public async Task UpdateCardAsync_WhenBoardColumnIdChanges_ShouldMoveCardToTopOfTargetColumn()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Move me", "source")
            .AddColumn("Doing")
            .AddCard("A", "1")
            .AddCard("B", "2")
            .Build();
        var movingCardId = board.GetCard("Todo", "Move me").Id;
        var doingColumnId = board.GetColumn("Doing").Id;
        var systemCardTypeId = await GetSystemCardTypeIdForBoardAsync(board.BoardId);

        // Act
        var service = CreateService();
        var result = await service.UpdateCardAsync(1, movingCardId, new UpdateCardRequest(
            Title: "Move me updated",
            Description: "updated",
            TagNames: [],
            CardTypeId: systemCardTypeId,
            BoardColumnId: doingColumnId), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(doingColumnId, result.Data!.BoardColumnId);
        Assert.Equal("Move me updated", result.Data.Title);
        Assert.Equal("updated", result.Data.Description);
        var doingTitles = await GetOrderedTitlesAsync(DbContextForAssert, doingColumnId);
        Assert.Equal(["Move me updated", "A", "B"], doingTitles);
    }

    [Fact]
    public async Task UpdateCardAsync_WhenBoardColumnIdMatchesCurrentColumn_ShouldNotReorderCards()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("A", "1")
            .AddCard("Middle", "2")
            .AddCard("B", "3")
            .Build();
        var middleCardId = board.GetCard("Todo", "Middle").Id;
        var todoColumnId = board.GetColumn("Todo").Id;
        var systemCardTypeId = await GetSystemCardTypeIdForBoardAsync(board.BoardId);

        // Act
        var service = CreateService();
        var result = await service.UpdateCardAsync(1, middleCardId, new UpdateCardRequest(
            Title: "Middle updated",
            Description: "2",
            TagNames: [],
            CardTypeId: systemCardTypeId,
            BoardColumnId: todoColumnId), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(todoColumnId, result.Data!.BoardColumnId);
        var todoTitles = await GetOrderedTitlesAsync(DbContextForAssert, todoColumnId);
        Assert.Equal(["A", "Middle updated", "B"], todoTitles);
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
    public async Task UpdateCardAsync_WhenTargetColumnMissing_ShouldReturnValidationErrorForBoardColumnId()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Card", "Desc")
            .AddColumn("Doing")
            .Build();
        var cardId = board.GetCard("Todo", "Card").Id;
        var systemCardTypeId = await GetSystemCardTypeIdForBoardAsync(board.BoardId);

        // Act
        var service = CreateService();
        var result = await service.UpdateCardAsync(1, cardId, new UpdateCardRequest(
            Title: "Card",
            Description: "Desc",
            TagNames: [],
            CardTypeId: systemCardTypeId,
            BoardColumnId: 999_999), ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("boardColumnId"));
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
    public async Task ArchiveCardAsync_WhenCardExists_ShouldSnapshotAndRemoveLiveCard()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Archive me", "Keep this")
            .AddColumn("Doing")
            .Build();
        var cardId = board.GetCard("Todo", "Archive me").Id;
        var boardId = board.BoardId;
        var systemCardTypeId = await GetSystemCardTypeIdForBoardAsync(boardId);
        await SeedTagsForArrangeAsync(boardId, "Bug", "Urgent");
        var taggingService = CreateService();
        var taggingResult = await taggingService.UpdateCardAsync(
            boardId,
            cardId,
            new UpdateCardRequest("Archive me", "Keep this", ["Urgent", "Bug"], systemCardTypeId),
            ActorUserId);
        Assert.True(taggingResult.Success);

        // Act
        var service = CreateService();
        var result = await service.ArchiveCardAsync(boardId, cardId, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(boardId, result.Data!.BoardId);
        Assert.Equal(cardId, result.Data.OriginalCardId);
        Assert.Equal("Archive me", result.Data.Title);
        Assert.Equal(["Bug", "Urgent"], result.Data.TagNames);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.SnapshotJson));

        var liveExists = await DbContextForAssert.Cards.AnyAsync(x => x.Id == cardId);
        Assert.False(liveExists);
        var archived = await DbContextForAssert.Set<ArchivedCardEntity>().SingleAsync();
        Assert.Equal(boardId, archived.BoardId);
        Assert.Equal(cardId, archived.OriginalCardId);
        Assert.Equal("Archive me", archived.SearchTitle);
        Assert.Equal("[\"Bug\",\"Urgent\"]", archived.SearchTagsJson);
        Assert.Contains("ARCHIVE ME", archived.SearchTextNormalised);
        Assert.Contains("URGENT", archived.SearchTextNormalised);
        Assert.Contains("\"version\":1", archived.SnapshotJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetArchivedCardsAsync_WhenMultipleCardsArchived_ShouldReturnNewestFirst()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Alpha", "First")
            .AddCard("Beta", "Second")
            .Build();
        var boardId = board.BoardId;
        var alphaCardId = board.GetCard("Todo", "Alpha").Id;
        var betaCardId = board.GetCard("Todo", "Beta").Id;
        var service = CreateService();
        var firstArchive = await service.ArchiveCardAsync(boardId, alphaCardId, ActorUserId);
        Assert.True(firstArchive.Success);
        var secondArchive = await service.ArchiveCardAsync(boardId, betaCardId, ActorUserId);
        Assert.True(secondArchive.Success);

        // Act
        var result = await service.GetArchivedCardsAsync(boardId, search: null, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(["Beta", "Alpha"], result.Data!.Select(x => x.Title).ToArray());
    }

    [Fact]
    public async Task GetArchivedCardsAsync_WhenSearchProvided_ShouldFilterByTitleAndTagsOnly()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Alpha feature", "No marker")
            .AddCard("Bravo card", "Contains needle in description")
            .Build();
        var boardId = board.BoardId;
        var alphaCardId = board.GetCard("Todo", "Alpha feature").Id;
        var bravoCardId = board.GetCard("Todo", "Bravo card").Id;
        await SeedTagsForArrangeAsync(boardId, "Urgent");
        var systemCardTypeId = await GetSystemCardTypeIdForBoardAsync(boardId);
        var taggingService = CreateService();
        var taggingResult = await taggingService.UpdateCardAsync(
            boardId,
            bravoCardId,
            new UpdateCardRequest("Bravo card", "Contains needle in description", ["Urgent"], systemCardTypeId),
            ActorUserId);
        Assert.True(taggingResult.Success);
        var service = CreateService();
        var alphaArchive = await service.ArchiveCardAsync(boardId, alphaCardId, ActorUserId);
        Assert.True(alphaArchive.Success);
        var bravoArchive = await service.ArchiveCardAsync(boardId, bravoCardId, ActorUserId);
        Assert.True(bravoArchive.Success);

        // Act
        var titleSearchResult = await service.GetArchivedCardsAsync(boardId, search: "alpha", ActorUserId);
        var tagSearchResult = await service.GetArchivedCardsAsync(boardId, search: "urgent", ActorUserId);
        var descriptionSearchResult = await service.GetArchivedCardsAsync(boardId, search: "needle", ActorUserId);

        // Assert
        Assert.True(titleSearchResult.Success);
        Assert.NotNull(titleSearchResult.Data);
        var archivedCard = Assert.Single(titleSearchResult.Data!);
        Assert.Equal("Alpha feature", archivedCard.Title);

        Assert.True(descriptionSearchResult.Success);
        Assert.NotNull(descriptionSearchResult.Data);
        Assert.Empty(descriptionSearchResult.Data!);

        Assert.True(tagSearchResult.Success);
        Assert.NotNull(tagSearchResult.Data);
        var tagMatch = Assert.Single(tagSearchResult.Data!);
        Assert.Equal("Bravo card", tagMatch.Title);
    }

    [Fact]
    public async Task GetArchivedCardsAsync_WhenSnapshotVersionIsUnknownNewer_ShouldStillReturnMetadata()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var boardId = board.BoardId;
        DbContextForArrange.Set<ArchivedCardEntity>().Add(new ArchivedCardEntity
        {
            BoardId = boardId,
            OriginalCardId = 123,
            ArchivedAtUtc = DateTime.UtcNow,
            SnapshotJson = """
                {"schema":"archived-card","version":999,"capturedAtUtc":"2026-04-19T16:00:00Z","payload":{"title":"Future"}}
                """,
            SearchTitle = "Future card",
            SearchTagsJson = """["Ops"]""",
            SearchTextNormalised = "FUTURE CARD\nOPS"
        });
        await DbContextForArrange.SaveChangesAsync();

        // Act
        var service = CreateService();
        var result = await service.GetArchivedCardsAsync(boardId, search: "ops", ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var card = Assert.Single(result.Data!);
        Assert.Equal("Future card", card.Title);
        Assert.Equal(["Ops"], card.TagNames);
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
        var archivedCount = await DbContextForAssert.Set<ArchivedCardEntity>().CountAsync();

        Assert.False(exists);
        Assert.Equal(0, archivedCount);
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
        var result = await service.CreateCardAsync(1, new CreateCardRequest(todoColumnId, "bad\ntitle", "Desc", null), ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("title"));
    }

    [Fact]
    public async Task CreateCardAsync_WhenTitleContainsSpecialCharacters_ShouldCreateCard()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();
        var todoColumnId = board.GetColumn("Todo").Id;
        var title = "Fix: titles with * < > & symbols";

        // Act
        var service = CreateService();
        var result = await service.CreateCardAsync(1, new CreateCardRequest(todoColumnId, title, "Desc", null), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(title, result.Data!.Title);
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

        var longDescription = new string('D', MaxCardDescriptionLength + 1);

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
    public async Task CreateCardAsync_WhenDescriptionAtMaxLength_ShouldCreateCard()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();
        var todoColumnId = board.GetColumn("Todo").Id;
        var maxLengthDescription = new string('D', MaxCardDescriptionLength);

        // Act
        var service = CreateService();
        var result = await service.CreateCardAsync(1, new CreateCardRequest(todoColumnId, "ValidTitle", maxLengthDescription, null), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(maxLengthDescription, result.Data!.Description);
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
        Assert.Equal(["Bug,Urgent"], result.Data!.Tags.Select(x => x.Name).ToArray());
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
