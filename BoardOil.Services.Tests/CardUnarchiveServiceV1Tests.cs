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
        var unarchiveResult = await service.UnarchiveCardAsync(boardId, archivedCard.OriginalCardId, ActorUserId);

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
    public async Task UnarchiveCardAsync_WithFrozenLegacyV1Json_ShouldRestoreUsingArchiveMetadataIdentity()
    {
        // Arrange
        const int archivedBoardCardId = 45_678;
        const int snapshotOriginalCardId = 777;
        const string frozenLegacyV1Json =
            """
            {
              "schema": "archived-card",
              "version": 1,
              "capturedAtUtc": "2026-04-26T12:00:00Z",
              "payload": {
                "boardId": 999,
                "originalCardId": 777,
                "boardColumnId": 999,
                "originalColumnName": "Legacy Todo",
                "cardTypeId": 999,
                "cardTypeName": "Legacy Story",
                "cardTypeEmoji": "📌",
                "title": "Frozen legacy V1",
                "description": "Legacy description",
                "sortKey": "A",
                "tags": [
                  {
                    "id": 21,
                    "name": "Legacy",
                    "styleName": "auto",
                    "stylePropertiesJson": "{}",
                    "emoji": null
                  }
                ],
                "tagNames": ["Legacy"],
                "createdAtUtc": "2026-04-25T10:00:00Z",
                "updatedAtUtc": "2026-04-26T11:00:00Z",
                "assignedUserId": null,
                "comments": [
                  {
                    "text": "Legacy comment",
                    "createdAtUtc": "2026-04-26T10:00:00Z",
                    "authorUserId": null,
                    "authorEmail": null
                  }
                ],
                "slickId": null,
                "slickName": "Legacy slick",
                "externalUrl": "https://example.test/legacy"
              }
            }
            """;
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var archivedCard = DbContextForArrange.Set<ArchivedCardEntity>().Add(new ArchivedCardEntity
        {
            BoardId = board.BoardId,
            OriginalCardId = archivedBoardCardId,
            ArchivedAtUtc = new DateTime(2026, 4, 26, 12, 0, 0, DateTimeKind.Utc),
            SnapshotJson = frozenLegacyV1Json,
            SearchTitle = "Frozen legacy V1",
            SearchTagsJson = "[\"Legacy\"]",
            SearchTextNormalised = "FROZEN LEGACY V1\nLEGACY"
        }).Entity;
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardArchiveService>();

        // Act
        var result = await service.UnarchiveCardAsync(board.BoardId, archivedBoardCardId, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(archivedBoardCardId, result.Data!.Id);
        Assert.NotEqual(snapshotOriginalCardId, result.Data.Id);
        Assert.Equal("Frozen legacy V1", result.Data.Title);
        Assert.Equal("Legacy description", result.Data.Description);
        Assert.Equal(["Legacy"], result.Data.TagNames);
        Assert.Equal("Legacy slick", result.Data.SlickName);
        Assert.Equal("https://example.test/legacy", result.Data.ExternalUrl);

        var restoredCard = await DbContextForAssert.Cards
            .Include(x => x.Comments)
            .SingleAsync(x => x.BoardId == board.BoardId && x.BoardCardId == archivedBoardCardId);
        var restoredComment = Assert.Single(restoredCard.Comments);
        Assert.Equal("Legacy comment", restoredComment.Text);
        Assert.False(await DbContextForAssert.Set<ArchivedCardEntity>().AnyAsync(x => x.Id == archivedCard.Id));
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
        var result = await service.UnarchiveCardAsync(boardId, archivedCard.OriginalCardId, ActorUserId);

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
        var unarchiveResult = await service.UnarchiveCardAsync(boardId, archivedCard.OriginalCardId, ActorUserId);

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
    public async Task UnarchiveCardAsync_WhenAssignedUserEmailMatchesActiveBoardMember_ShouldRestoreAssignment()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var archivedCard = await SeedArchivedCardV1Async(
            board.BoardId,
            originalCardId: 779,
            boardColumnId: board.GetColumn("Todo").Id,
            cardTypeId: await GetSystemCardTypeIdForBoardAsync(board.BoardId),
            title: "Archived with portable assignee",
            description: "Desc",
            assignedUserId: 999_999,
            assignedUserEmail: "ACTOR@LOCALHOST");
        var service = ResolveService<ICardArchiveService>();

        // Act
        var result = await service.UnarchiveCardAsync(board.BoardId, archivedCard.OriginalCardId, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(ActorUserId, result.Data!.AssignedUserId);
    }

    [Fact]
    public async Task UnarchiveCardAsync_WhenLegacySnapshotHasOnlyAssignedUserId_ShouldLeaveCardUnassigned()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var archivedCard = await SeedArchivedCardV1Async(
            board.BoardId,
            originalCardId: 780,
            boardColumnId: board.GetColumn("Todo").Id,
            cardTypeId: await GetSystemCardTypeIdForBoardAsync(board.BoardId),
            title: "Legacy archived assignee",
            description: "Desc",
            assignedUserId: ActorUserId);
        var service = ResolveService<ICardArchiveService>();

        // Act
        var result = await service.UnarchiveCardAsync(board.BoardId, archivedCard.OriginalCardId, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Null(result.Data!.AssignedUserId);
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
        var unarchiveResult = await service.UnarchiveCardAsync(boardId, archivedCard.OriginalCardId, ActorUserId);

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
        var result = await service.UnarchiveCardAsync(board.BoardId, boardCardId: 999_999, ActorUserId);

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
        var unarchiveResult = await service.UnarchiveCardAsync(boardId, archivedCard.OriginalCardId, ActorUserId);

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
        var unarchiveResult = await service.UnarchiveCardAsync(boardId, archivedCard.OriginalCardId, ActorUserId);

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
        var result = await service.UnarchiveCardAsync(board.BoardId, archivedCard.OriginalCardId, ActorUserId);

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
        IReadOnlyList<ArchivedCardSnapshotCommentV1Payload>? comments = null,
        int? assignedUserId = null,
        string? assignedUserEmail = null)
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
            comments,
            assignedUserId,
            assignedUserEmail);
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
        IReadOnlyList<ArchivedCardSnapshotCommentV1Payload>? comments = null,
        int? assignedUserId = null,
        string? assignedUserEmail = null)
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
            assignedUserId,
            comments,
            AssignedUserEmail: assignedUserEmail);
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
        var result = await service.UnarchiveCardAsync(board.BoardId, archivedCard.OriginalCardId, ActorUserId);

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
