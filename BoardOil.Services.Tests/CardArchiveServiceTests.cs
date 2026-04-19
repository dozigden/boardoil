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
        var service = ResolveService<ICardArchiveService>();
        var result = await service.GetArchivedCardsAsync(boardId, search: "ops", ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var card = Assert.Single(result.Data!);
        Assert.Equal("Future card", card.Title);
        Assert.Equal(["Ops"], card.TagNames);
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
