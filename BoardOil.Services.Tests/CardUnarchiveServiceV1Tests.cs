using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Card;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Card;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit;
using ArchivedCardEntity = BoardOil.Data.Abstractions.Entities.EntityArchivedCard;

namespace BoardOil.Services.Tests;

public sealed class CardUnarchiveServiceV1Tests : TestBaseDb
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task UnarchiveCardAsync_WhenArchivedCardV1Exists_ShouldRestoreLiveCardAndRemoveArchive()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var boardId = board.BoardId;
        var todoColumnId = board.GetColumn("Todo").Id;
        var archivedCard = await SeedArchivedCardV1Async(
            boardId,
            originalCardId: 12345,
            boardColumnId: todoColumnId,
            cardTypeId: await GetSystemCardTypeIdForBoardAsync(boardId),
            title: "Archive me",
            description: "Desc");
        var boardEvents = Assert.IsType<TestBoardEvents>(ResolveService<BoardOil.Abstractions.IBoardEvents>());
        var service = ResolveService<ICardArchiveService>();

        // Act
        var unarchiveResult = await service.UnarchiveCardAsync(boardId, archivedCard.Id, ActorUserId);

        // Assert
        Assert.True(unarchiveResult.Success);
        Assert.NotNull(unarchiveResult.Data);
        Assert.Equal(archivedCard.OriginalCardId, unarchiveResult.Data!.Id);
        Assert.Equal("Archive me", unarchiveResult.Data.Title);
        Assert.Equal("Desc", unarchiveResult.Data.Description);
        Assert.Equal(todoColumnId, unarchiveResult.Data.BoardColumnId);
        Assert.Empty(await DbContextForAssert.Set<ArchivedCardEntity>().ToListAsync());
        Assert.Single(boardEvents.CardCreatedEvents);
        Assert.Empty(boardEvents.ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task UnarchiveCardAsync_WhenLeadingKeysAreExhausted_ShouldRenormaliseAndRestoreAtTop()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("A", "1")
            .AddCard("B", "2")
            .Build();
        var boardId = board.BoardId;
        var todoColumnId = board.GetColumn("Todo").Id;
        var cardA = await DbContextForArrange.Cards.SingleAsync(card => card.Id == board.GetCard("Todo", "A").Id);
        var cardB = await DbContextForArrange.Cards.SingleAsync(card => card.Id == board.GetCard("Todo", "B").Id);
        cardA.SortKey = "00000000000000000000";
        cardB.SortKey = "00000000000000000001";
        await DbContextForArrange.SaveChangesAsync();
        var archivedCard = await SeedArchivedCardV1Async(
            boardId,
            originalCardId: 12346,
            boardColumnId: todoColumnId,
            cardTypeId: await GetSystemCardTypeIdForBoardAsync(boardId),
            title: "Restored",
            description: "Desc");
        var boardEvents = Assert.IsType<TestBoardEvents>(ResolveService<BoardOil.Abstractions.IBoardEvents>());
        var service = ResolveService<ICardArchiveService>();

        // Act
        var result = await service.UnarchiveCardAsync(boardId, archivedCard.Id, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(["Restored", "A", "B"], await GetOrderedTitlesAsync(DbContextForAssert, todoColumnId));
        var storedKeys = await DbContextForAssert.Cards
            .Where(card => card.BoardColumnId == todoColumnId)
            .OrderBy(card => card.SortKey)
            .Select(card => card.SortKey)
            .ToListAsync();
        Assert.Equal(3, storedKeys.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain("00000000000000000000", storedKeys);
        Assert.DoesNotContain("00000000000000000001", storedKeys);
        Assert.Empty(await DbContextForAssert.Set<ArchivedCardEntity>().ToListAsync());
        Assert.Single(boardEvents.CardCreatedEvents);
        Assert.Equal([boardId], boardEvents.ResyncRequestedBoardIds);
        Assert.Equal(["card-created", "resync-requested"], boardEvents.PublishedEventNames);
    }

    [Fact]
    public async Task UnarchiveCardAsync_WhenArchivedCardV1ContainsComments_ShouldRestoreComments()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var boardId = board.BoardId;
        var todoColumnId = board.GetColumn("Todo").Id;
        var capturedAtUtc = new DateTime(2026, 4, 26, 12, 0, 0, DateTimeKind.Utc);
        var archivedCard = await SeedArchivedCardV1Async(
            boardId,
            originalCardId: 777,
            boardColumnId: todoColumnId,
            cardTypeId: await GetSystemCardTypeIdForBoardAsync(boardId),
            title: "Archived with comments",
            description: "Desc",
            capturedAtUtc: capturedAtUtc,
            comments:
            [
                new ArchivedCardSnapshotCommentV1Payload(
                    "Known author comment",
                    capturedAtUtc,
                    ActorUserId,
                    null),
                new ArchivedCardSnapshotCommentV1Payload(
                    "Unknown author comment",
                    capturedAtUtc.AddMinutes(1),
                    null,
                    null)
            ]);
        var service = ResolveService<ICardArchiveService>();

        // Act
        var unarchiveResult = await service.UnarchiveCardAsync(boardId, archivedCard.Id, ActorUserId);

        // Assert
        Assert.True(unarchiveResult.Success);
        Assert.NotNull(unarchiveResult.Data);
        var restoredInternalCardId = await DbContextForAssert.Cards
            .Where(x => x.BoardId == boardId && x.BoardCardId == unarchiveResult.Data!.Id)
            .Select(x => x.Id)
            .SingleAsync();
        var restoredComments = await DbContextForAssert.CardComments
            .Where(x => x.CardId == restoredInternalCardId)
            .OrderBy(x => x.PostedAtUtc)
            .ToListAsync();
        Assert.Equal(2, restoredComments.Count);
        Assert.Equal("Known author comment", restoredComments[0].Text);
        Assert.Equal(ActorUserId, restoredComments[0].AuthorUserId);
        Assert.Equal("Unknown author comment", restoredComments[1].Text);
        Assert.Null(restoredComments[1].AuthorUserId);
    }

    [Fact]
    public async Task UnarchiveCardAsync_WhenArchivedCardV1CommentAuthorFallsBackToEmail_ShouldRelinkAuthor()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var boardId = board.BoardId;
        var todoColumnId = board.GetColumn("Todo").Id;
        var capturedAtUtc = new DateTime(2026, 4, 26, 12, 0, 0, DateTimeKind.Utc);
        var archivedCard = await SeedArchivedCardV1Async(
            boardId,
            originalCardId: 778,
            boardColumnId: todoColumnId,
            cardTypeId: await GetSystemCardTypeIdForBoardAsync(boardId),
            title: "Archived with email-linked comment",
            description: "Desc",
            capturedAtUtc: capturedAtUtc,
            comments:
            [
                new ArchivedCardSnapshotCommentV1Payload(
                    "Email linked comment",
                    capturedAtUtc,
                    999_999,
                    "ACTOR@LOCALHOST")
            ]);
        var service = ResolveService<ICardArchiveService>();

        // Act
        var unarchiveResult = await service.UnarchiveCardAsync(boardId, archivedCard.Id, ActorUserId);

        // Assert
        Assert.True(unarchiveResult.Success);
        Assert.NotNull(unarchiveResult.Data);
        var restoredInternalCardId = await DbContextForAssert.Cards
            .Where(x => x.BoardId == boardId && x.BoardCardId == unarchiveResult.Data!.Id)
            .Select(x => x.Id)
            .SingleAsync();
        var restoredComment = await DbContextForAssert.CardComments
            .SingleAsync(x => x.CardId == restoredInternalCardId);
        Assert.Equal("Email linked comment", restoredComment.Text);
        Assert.Equal(ActorUserId, restoredComment.AuthorUserId);
    }

    [Fact]
    public async Task UnarchiveCardAsync_WhenArchivedCardMissing_ShouldReturnNotFound()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var service = ResolveService<ICardArchiveService>();

        // Act
        var result = await service.UnarchiveCardAsync(board.BoardId, archivedCardId: 999_999, ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task UnarchiveCardAsync_WhenOriginalColumnMissingInV1Snapshot_ShouldFallbackToFirstColumn()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();
        var boardId = board.BoardId;
        var todoColumnId = board.GetColumn("Todo").Id;
        var systemCardTypeId = await GetSystemCardTypeIdForBoardAsync(boardId);
        var archivedCard = await SeedArchivedCardV1Async(
            boardId,
            originalCardId: 12346,
            boardColumnId: 999999,
            cardTypeId: systemCardTypeId,
            title: "Archive me",
            description: "Desc");
        var service = ResolveService<ICardArchiveService>();

        // Act
        var unarchiveResult = await service.UnarchiveCardAsync(boardId, archivedCard.Id, ActorUserId);

        // Assert
        Assert.True(unarchiveResult.Success);
        Assert.NotNull(unarchiveResult.Data);
        Assert.Equal(todoColumnId, unarchiveResult.Data!.BoardColumnId);
    }

    [Fact]
    public async Task UnarchiveCardAsync_WhenV1SnapshotCardTypeMissing_ShouldFallbackToSystemCardType()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var boardId = board.BoardId;
        var now = DateTime.UtcNow;
        var customCardType = DbContextForArrange.CardTypes.Add(new EntityCardType
        {
            BoardId = boardId,
            Name = "Bug",
            Emoji = "🐛",
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#224466","textColorMode":"auto"}""",
            IsSystem = false,
        }).Entity;
        await DbContextForArrange.SaveChangesAsync();

        var systemCardTypeId = await GetSystemCardTypeIdForBoardAsync(boardId);
        var archivedCard = await SeedArchivedCardV1Async(
            boardId,
            originalCardId: 12347,
            boardColumnId: board.GetColumn("Todo").Id,
            cardTypeId: customCardType.Id,
            title: "Archive me",
            description: "Desc");

        DbContextForArrange.CardTypes.Remove(customCardType);
        await DbContextForArrange.SaveChangesAsync();

        var service = ResolveService<ICardArchiveService>();

        // Act
        var unarchiveResult = await service.UnarchiveCardAsync(boardId, archivedCard.Id, ActorUserId);

        // Assert
        Assert.True(unarchiveResult.Success);
        Assert.NotNull(unarchiveResult.Data);
        Assert.Equal(systemCardTypeId, unarchiveResult.Data!.CardTypeId);
    }

    [Fact]
    public async Task UnarchiveCardAsync_WhenV1SnapshotInvalid_ShouldReturnBadRequest()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var archivedCard = DbContextForArrange.Set<ArchivedCardEntity>().Add(new ArchivedCardEntity
        {
            BoardId = board.BoardId,
            OriginalCardId = 123,
            ArchivedAtUtc = DateTime.UtcNow,
            SnapshotJson = "not-json",
            SearchTitle = "Broken snapshot",
            SearchTagsJson = "[]",
            SearchTextNormalised = "BROKEN SNAPSHOT"
        }).Entity;
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardArchiveService>();

        // Act
        var result = await service.UnarchiveCardAsync(board.BoardId, archivedCard.Id, ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    private async Task<ArchivedCardEntity> SeedArchivedCardV1Async(
        int boardId,
        int originalCardId,
        int boardColumnId,
        int cardTypeId,
        string title,
        string description,
        DateTime? capturedAtUtc = null,
        IReadOnlyList<ArchivedCardSnapshotCommentV1Payload>? comments = null)
    {
        var archivedAtUtc = capturedAtUtc ?? new DateTime(2026, 4, 26, 12, 0, 0, DateTimeKind.Utc);
        var snapshotJson = CreateSnapshotJsonV1(
            boardId,
            originalCardId,
            boardColumnId,
            cardTypeId,
            title,
            description,
            archivedAtUtc,
            comments);
        var archivedCard = DbContextForArrange.Set<ArchivedCardEntity>().Add(new ArchivedCardEntity
        {
            BoardId = boardId,
            OriginalCardId = originalCardId,
            ArchivedAtUtc = archivedAtUtc,
            SnapshotJson = snapshotJson,
            SearchTitle = title,
            SearchTagsJson = "[]",
            SearchTextNormalised = title.ToUpperInvariant()
        }).Entity;
        await DbContextForArrange.SaveChangesAsync();
        return archivedCard;
    }

    private static string CreateSnapshotJsonV1(
        int boardId,
        int originalCardId,
        int boardColumnId,
        int cardTypeId,
        string title,
        string description,
        DateTime capturedAtUtc,
        IReadOnlyList<ArchivedCardSnapshotCommentV1Payload>? comments = null)
    {
        var payload = new ArchivedCardSnapshotV1Payload(
            boardId,
            originalCardId,
            boardColumnId,
            "Todo",
            cardTypeId,
            "Story",
            null,
            title,
            description,
            "A",
            [],
            [],
            capturedAtUtc,
            capturedAtUtc,
            null,
            comments);
        var envelope = new ArchivedCardSnapshotEnvelopeV1(
            ArchivedCardSnapshotSerialiser.SchemaName,
            1,
            capturedAtUtc,
            payload);
        return JsonSerializer.Serialize(envelope, SnapshotJsonOptions);
    }

    private Task<int> GetSystemCardTypeIdForBoardAsync(int boardId) =>
        DbContextForArrange.CardTypes
            .Where(x => x.BoardId == boardId && x.IsSystem)
            .Select(x => x.Id)
            .SingleAsync();
}

public sealed class CardUnarchiveServiceV1AuthorisationTests : TestBaseDb
{
    private readonly CapturingBoardAuthorisationService _boardAuthorisationService = new();

    [Fact]
    public async Task UnarchiveCardAsync_WhenPermissionDenied_ShouldCheckCardCreatePermission()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var archivedCard = DbContextForArrange.Set<ArchivedCardEntity>().Add(new ArchivedCardEntity
        {
            BoardId = board.BoardId,
            OriginalCardId = 123,
            ArchivedAtUtc = DateTime.UtcNow,
            SnapshotJson = CreateAuthorisationSnapshotJsonV1(),
            SearchTitle = "Archive me",
            SearchTagsJson = "[]",
            SearchTextNormalised = "ARCHIVE ME"
        }).Entity;
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardArchiveService>();

        // Act
        var result = await service.UnarchiveCardAsync(board.BoardId, archivedCard.Id, ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(BoardPermission.CardCreate, _boardAuthorisationService.LastPermission);
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton(_boardAuthorisationService);
        services.AddScoped<IBoardAuthorisationService>(provider =>
            provider.GetRequiredService<CapturingBoardAuthorisationService>());
    }

    private static string CreateAuthorisationSnapshotJsonV1() =>
        """
        {"schema":"archived-card","version":1,"capturedAtUtc":"2026-04-26T12:00:00Z","payload":{"boardId":1,"originalCardId":123,"boardColumnId":1,"originalColumnName":"Todo","cardTypeId":1,"cardTypeName":"Story","cardTypeEmoji":null,"title":"Archive me","description":"Desc","sortKey":"A","tags":[],"tagNames":[],"createdAtUtc":"2026-04-26T12:00:00Z","updatedAtUtc":"2026-04-26T12:00:00Z","assignedUserId":null}}
        """;

    private sealed class CapturingBoardAuthorisationService : IBoardAuthorisationService
    {
        public BoardPermission? LastPermission { get; private set; }

        public Task<bool> HasPermissionAsync(int boardId, int actorUserId, BoardPermission permission)
        {
            LastPermission = permission;
            return Task.FromResult(false);
        }
    }
}
