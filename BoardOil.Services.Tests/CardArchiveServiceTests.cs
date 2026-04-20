using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Card;
using BoardOil.Services.Card;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using ArchivedCardEntity = BoardOil.Persistence.Abstractions.Entities.EntityArchivedCard;
using TagEntity = BoardOil.Persistence.Abstractions.Entities.EntityTag;

namespace BoardOil.Services.Tests;

public sealed class CardArchiveServiceTests : TestBaseDb
{
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
        var taggingService = ResolveService<CardService>();
        var taggingResult = await taggingService.UpdateCardAsync(
            boardId,
            cardId,
            new UpdateCardRequest("Archive me", "Keep this", ["Urgent", "Bug"], systemCardTypeId),
            ActorUserId);
        Assert.True(taggingResult.Success);

        // Act
        var service = ResolveService<ICardArchiveService>();
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
    public async Task ArchiveCardsAsync_WhenCardsExist_ShouldArchiveAllAndReturnSummary()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Alpha", "First")
            .AddCard("Bravo", "Second")
            .Build();
        var boardId = board.BoardId;
        var alphaCardId = board.GetCard("Todo", "Alpha").Id;
        var bravoCardId = board.GetCard("Todo", "Bravo").Id;
        var service = ResolveService<ICardArchiveService>();

        // Act
        var result = await service.ArchiveCardsAsync(boardId, new ArchiveCardsRequest([alphaCardId, bravoCardId]), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(boardId, result.Data!.BoardId);
        Assert.Equal(2, result.Data.RequestedCount);
        Assert.Equal(2, result.Data.ArchivedCount);

        var liveCards = await DbContextForAssert.Cards.Where(x => x.Id == alphaCardId || x.Id == bravoCardId).ToListAsync();
        Assert.Empty(liveCards);
        var archivedCards = await DbContextForAssert.Set<ArchivedCardEntity>()
            .Where(x => x.BoardId == boardId)
            .ToListAsync();
        Assert.Equal(2, archivedCards.Count);
    }

    [Fact]
    public async Task ArchiveCardsAsync_WhenCardIdsContainDuplicates_ShouldReturnBadRequest()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Duplicate", "Desc")
            .Build();
        var boardId = board.BoardId;
        var cardId = board.GetCard("Todo", "Duplicate").Id;
        var service = ResolveService<ICardArchiveService>();

        // Act
        var result = await service.ArchiveCardsAsync(boardId, new ArchiveCardsRequest([cardId, cardId]), ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("cardIds", result.ValidationErrors!.Keys, StringComparer.Ordinal);

        var liveCard = await DbContextForAssert.Cards.SingleAsync(x => x.Id == cardId);
        Assert.Equal("Duplicate", liveCard.Title);
        var archivedCount = await DbContextForAssert.Set<ArchivedCardEntity>().CountAsync();
        Assert.Equal(0, archivedCount);
    }

    [Fact]
    public async Task ArchiveCardsAsync_WhenAnyCardIsMissing_ShouldReturnNotFoundAndArchiveNothing()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Alpha", "First")
            .AddCard("Bravo", "Second")
            .Build();
        var boardId = board.BoardId;
        var alphaCardId = board.GetCard("Todo", "Alpha").Id;
        var bravoCardId = board.GetCard("Todo", "Bravo").Id;
        var missingCardId = bravoCardId + 10_000;
        var service = ResolveService<ICardArchiveService>();

        // Act
        var result = await service.ArchiveCardsAsync(boardId, new ArchiveCardsRequest([alphaCardId, missingCardId]), ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);

        var liveCardIds = await DbContextForAssert.Cards
            .Where(x => x.Id == alphaCardId || x.Id == bravoCardId)
            .Select(x => x.Id)
            .ToListAsync();
        Assert.Equal(2, liveCardIds.Count);
        var archivedCount = await DbContextForAssert.Set<ArchivedCardEntity>().CountAsync();
        Assert.Equal(0, archivedCount);
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
        var service = ResolveService<ICardArchiveService>();
        var firstArchive = await service.ArchiveCardAsync(boardId, alphaCardId, ActorUserId);
        Assert.True(firstArchive.Success);
        var secondArchive = await service.ArchiveCardAsync(boardId, betaCardId, ActorUserId);
        Assert.True(secondArchive.Success);

        // Act
        var result = await service.GetArchivedCardsAsync(boardId, search: null, offset: null, limit: null, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(["Beta", "Alpha"], result.Data!.Items.Select(x => x.Title).ToArray());
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
        var taggingService = ResolveService<CardService>();
        var taggingResult = await taggingService.UpdateCardAsync(
            boardId,
            bravoCardId,
            new UpdateCardRequest("Bravo card", "Contains needle in description", ["Urgent"], systemCardTypeId),
            ActorUserId);
        Assert.True(taggingResult.Success);
        var service = ResolveService<ICardArchiveService>();
        var alphaArchive = await service.ArchiveCardAsync(boardId, alphaCardId, ActorUserId);
        Assert.True(alphaArchive.Success);
        var bravoArchive = await service.ArchiveCardAsync(boardId, bravoCardId, ActorUserId);
        Assert.True(bravoArchive.Success);

        // Act
        var titleSearchResult = await service.GetArchivedCardsAsync(boardId, search: "alpha", offset: null, limit: null, ActorUserId);
        var tagSearchResult = await service.GetArchivedCardsAsync(boardId, search: "urgent", offset: null, limit: null, ActorUserId);
        var descriptionSearchResult = await service.GetArchivedCardsAsync(boardId, search: "needle", offset: null, limit: null, ActorUserId);

        // Assert
        Assert.True(titleSearchResult.Success);
        Assert.NotNull(titleSearchResult.Data);
        var archivedCard = Assert.Single(titleSearchResult.Data!.Items);
        Assert.Equal("Alpha feature", archivedCard.Title);

        Assert.True(descriptionSearchResult.Success);
        Assert.NotNull(descriptionSearchResult.Data);
        Assert.Empty(descriptionSearchResult.Data!.Items);

        Assert.True(tagSearchResult.Success);
        Assert.NotNull(tagSearchResult.Data);
        var tagMatch = Assert.Single(tagSearchResult.Data!.Items);
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
        var service = ResolveService<ICardArchiveService>();
        var result = await service.GetArchivedCardsAsync(boardId, search: "ops", offset: null, limit: null, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var card = Assert.Single(result.Data!.Items);
        Assert.Equal("Future card", card.Title);
        Assert.Equal(["Ops"], card.TagNames);
    }

    [Fact]
    public async Task GetArchivedCardsAsync_WhenPaginationSpecified_ShouldReturnSliceAndMetadata()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Alpha", "First")
            .AddCard("Bravo", "Second")
            .AddCard("Charlie", "Third")
            .Build();
        var boardId = board.BoardId;
        var service = ResolveService<ICardArchiveService>();
        var alphaArchive = await service.ArchiveCardAsync(boardId, board.GetCard("Todo", "Alpha").Id, ActorUserId);
        Assert.True(alphaArchive.Success);
        var bravoArchive = await service.ArchiveCardAsync(boardId, board.GetCard("Todo", "Bravo").Id, ActorUserId);
        Assert.True(bravoArchive.Success);
        var charlieArchive = await service.ArchiveCardAsync(boardId, board.GetCard("Todo", "Charlie").Id, ActorUserId);
        Assert.True(charlieArchive.Success);

        // Act
        var result = await service.GetArchivedCardsAsync(boardId, search: null, offset: 1, limit: 1, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data!.Offset);
        Assert.Equal(1, result.Data.Limit);
        Assert.Equal(3, result.Data.TotalCount);
        var archivedCard = Assert.Single(result.Data.Items);
        Assert.Equal("Bravo", archivedCard.Title);
    }

    [Fact]
    public async Task GetArchivedCardsAsync_WhenPaginationInvalid_ShouldReturnBadRequest()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var service = ResolveService<ICardArchiveService>();

        // Act
        var result = await service.GetArchivedCardsAsync(board.BoardId, search: null, offset: -1, limit: 0, ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("offset", result.ValidationErrors!.Keys, StringComparer.Ordinal);
        Assert.Contains("limit", result.ValidationErrors.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public async Task GetArchivedCardAsync_WhenArchivedCardExists_ShouldReturnFullSnapshot()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Archive me", "Desc")
            .Build();
        var boardId = board.BoardId;
        var cardId = board.GetCard("Todo", "Archive me").Id;
        var service = ResolveService<ICardArchiveService>();
        var archiveResult = await service.ArchiveCardAsync(boardId, cardId, ActorUserId);
        Assert.True(archiveResult.Success);
        Assert.NotNull(archiveResult.Data);

        // Act
        var result = await service.GetArchivedCardAsync(boardId, archiveResult.Data!.Id, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Archive me", result.Data!.Title);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.SnapshotJson));
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
