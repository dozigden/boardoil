using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Card;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ArchivedCardEntity = BoardOil.Data.Abstractions.Entities.EntityArchivedCard;

namespace BoardOil.Services.Tests;

public sealed class CardUnarchiveServiceV1Tests : TestBaseDb
{
    [Fact]
    public async Task UnarchiveCardAsync_WhenArchivedCardV1Exists_ShouldRestoreLiveCardAndRemoveArchive()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var boardId = board.BoardId;
        var todoColumnId = board.GetColumn("Todo").Id;
        var systemCardTypeId = await GetSystemCardTypeIdForBoardAsync(boardId);
        var snapshotJson =
            $$"""
            {
              "schema": "archived-card",
              "version": 1,
              "capturedAtUtc": "2026-04-26T12:00:00Z",
              "payload": {
                "boardId": {{boardId}},
                "originalCardId": 12345,
                "boardColumnId": {{todoColumnId}},
                "originalColumnName": "Todo",
                "cardTypeId": {{systemCardTypeId}},
                "cardTypeName": "Story",
                "cardTypeEmoji": null,
                "title": "Archive me",
                "description": "Desc",
                "sortKey": "A",
                "tags": [],
                "tagNames": [],
                "createdAtUtc": "2026-04-26T12:00:00Z",
                "updatedAtUtc": "2026-04-26T12:00:00Z",
                "assignedUserId": null
              }
            }
            """;
        var archivedCard = await SeedArchivedCardV1Async(
            boardId,
            originalCardId: 12345,
            title: "Archive me",
            snapshotJson: snapshotJson);
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
        Assert.Equal(new DateTime(2026, 4, 26, 12, 0, 0, DateTimeKind.Utc), unarchiveResult.Data.CardCreatedUtc);
        Assert.Equal(new DateTime(2026, 4, 26, 12, 0, 0, DateTimeKind.Utc), unarchiveResult.Data.CardUpdatedUtc);
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
        Assert.Null(result.Data.SlickName);
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
        var systemCardTypeId = await GetSystemCardTypeIdForBoardAsync(boardId);
        var snapshotJson =
            $$"""
            {
              "schema": "archived-card",
              "version": 1,
              "capturedAtUtc": "2026-04-26T12:00:00Z",
              "payload": {
                "boardId": {{boardId}},
                "originalCardId": 12346,
                "boardColumnId": {{todoColumnId}},
                "originalColumnName": "Todo",
                "cardTypeId": {{systemCardTypeId}},
                "cardTypeName": "Story",
                "cardTypeEmoji": null,
                "title": "Restored",
                "description": "Desc",
                "sortKey": "A",
                "tags": [],
                "tagNames": [],
                "createdAtUtc": "2026-04-26T12:00:00Z",
                "updatedAtUtc": "2026-04-26T12:00:00Z",
                "assignedUserId": null
              }
            }
            """;
        var archivedCard = await SeedArchivedCardV1Async(
            boardId,
            originalCardId: 12346,
            title: "Restored",
            snapshotJson: snapshotJson);
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
    public async Task UnarchiveCardAsync_WhenArchivedCardV1CommentsLackAuthorEmail_ShouldRestoreCommentsUnattributed()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var boardId = board.BoardId;
        var todoColumnId = board.GetColumn("Todo").Id;
        var capturedAtUtc = new DateTime(2026, 4, 26, 12, 0, 0, DateTimeKind.Utc);
        var systemCardTypeId = await GetSystemCardTypeIdForBoardAsync(boardId);
        var snapshotJson =
            $$"""
            {
              "schema": "archived-card",
              "version": 1,
              "capturedAtUtc": "2026-04-26T12:00:00Z",
              "payload": {
                "boardId": {{boardId}},
                "originalCardId": 777,
                "boardColumnId": {{todoColumnId}},
                "originalColumnName": "Todo",
                "cardTypeId": {{systemCardTypeId}},
                "cardTypeName": "Story",
                "cardTypeEmoji": null,
                "title": "Archived with comments",
                "description": "Desc",
                "sortKey": "A",
                "tags": [],
                "tagNames": [],
                "createdAtUtc": "2026-04-26T12:00:00Z",
                "updatedAtUtc": "2026-04-26T12:00:00Z",
                "assignedUserId": null,
                "comments": [
                  {
                    "text": "Known author comment",
                    "createdAtUtc": "2026-04-26T12:00:00Z",
                    "authorUserId": {{ActorUserId}},
                    "authorEmail": null
                  },
                  {
                    "text": "Unknown author comment",
                    "createdAtUtc": "2026-04-26T12:01:00Z",
                    "authorUserId": null,
                    "authorEmail": null
                  }
                ]
              }
            }
            """;
        var archivedCard = await SeedArchivedCardV1Async(
            boardId,
            originalCardId: 777,
            title: "Archived with comments",
            snapshotJson: snapshotJson,
            archivedAtUtc: capturedAtUtc);
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
        Assert.Null(restoredComments[0].AuthorUserId);
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
        var boardId = board.BoardId;
        var todoColumnId = board.GetColumn("Todo").Id;
        var systemCardTypeId = await GetSystemCardTypeIdForBoardAsync(boardId);
        var snapshotJson =
            $$"""
            {
              "schema": "archived-card",
              "version": 1,
              "capturedAtUtc": "2026-04-26T12:00:00Z",
              "payload": {
                "boardId": {{boardId}},
                "originalCardId": 779,
                "boardColumnId": {{todoColumnId}},
                "originalColumnName": "Todo",
                "cardTypeId": {{systemCardTypeId}},
                "cardTypeName": "Story",
                "cardTypeEmoji": null,
                "title": "Archived with portable assignee",
                "description": "Desc",
                "sortKey": "A",
                "tags": [],
                "tagNames": [],
                "createdAtUtc": "2026-04-26T12:00:00Z",
                "updatedAtUtc": "2026-04-26T12:00:00Z",
                "assignedUserId": 999999,
                "assignedUserEmail": "ACTOR@LOCALHOST"
              }
            }
            """;
        var archivedCard = await SeedArchivedCardV1Async(
            boardId,
            originalCardId: 779,
            title: "Archived with portable assignee",
            snapshotJson: snapshotJson);
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
        var boardId = board.BoardId;
        var todoColumnId = board.GetColumn("Todo").Id;
        var systemCardTypeId = await GetSystemCardTypeIdForBoardAsync(boardId);
        var snapshotJson =
            $$"""
            {
              "schema": "archived-card",
              "version": 1,
              "capturedAtUtc": "2026-04-26T12:00:00Z",
              "payload": {
                "boardId": {{boardId}},
                "originalCardId": 780,
                "boardColumnId": {{todoColumnId}},
                "originalColumnName": "Todo",
                "cardTypeId": {{systemCardTypeId}},
                "cardTypeName": "Story",
                "cardTypeEmoji": null,
                "title": "Legacy archived assignee",
                "description": "Desc",
                "sortKey": "A",
                "tags": [],
                "tagNames": [],
                "createdAtUtc": "2026-04-26T12:00:00Z",
                "updatedAtUtc": "2026-04-26T12:00:00Z",
                "assignedUserId": {{ActorUserId}}
              }
            }
            """;
        var archivedCard = await SeedArchivedCardV1Async(
            boardId,
            originalCardId: 780,
            title: "Legacy archived assignee",
            snapshotJson: snapshotJson);
        var service = ResolveService<ICardArchiveService>();

        // Act
        var result = await service.UnarchiveCardAsync(board.BoardId, archivedCard.OriginalCardId, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Null(result.Data!.AssignedUserId);
    }

    [Fact]
    public async Task UnarchiveCardAsync_WhenArchivedCardV1CommentAuthorEmailMatches_ShouldRelinkAuthor()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var boardId = board.BoardId;
        var todoColumnId = board.GetColumn("Todo").Id;
        var capturedAtUtc = new DateTime(2026, 4, 26, 12, 0, 0, DateTimeKind.Utc);
        var systemCardTypeId = await GetSystemCardTypeIdForBoardAsync(boardId);
        var snapshotJson =
            $$"""
            {
              "schema": "archived-card",
              "version": 1,
              "capturedAtUtc": "2026-04-26T12:00:00Z",
              "payload": {
                "boardId": {{boardId}},
                "originalCardId": 778,
                "boardColumnId": {{todoColumnId}},
                "originalColumnName": "Todo",
                "cardTypeId": {{systemCardTypeId}},
                "cardTypeName": "Story",
                "cardTypeEmoji": null,
                "title": "Archived with email-linked comment",
                "description": "Desc",
                "sortKey": "A",
                "tags": [],
                "tagNames": [],
                "createdAtUtc": "2026-04-26T12:00:00Z",
                "updatedAtUtc": "2026-04-26T12:00:00Z",
                "assignedUserId": null,
                "comments": [
                  {
                    "text": "Email linked comment",
                    "createdAtUtc": "2026-04-26T12:00:00Z",
                    "authorUserId": 999999,
                    "authorEmail": "ACTOR@LOCALHOST"
                  }
                ]
              }
            }
            """;
        var archivedCard = await SeedArchivedCardV1Async(
            boardId,
            originalCardId: 778,
            title: "Archived with email-linked comment",
            snapshotJson: snapshotJson,
            archivedAtUtc: capturedAtUtc);
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
        var doingColumnId = board.GetColumn("Doing").Id;
        var systemCardTypeId = await GetSystemCardTypeIdForBoardAsync(boardId);
        var snapshotJson =
            $$"""
            {
              "schema": "archived-card",
              "version": 1,
              "capturedAtUtc": "2026-04-26T12:00:00Z",
              "payload": {
                "boardId": {{boardId}},
                "originalCardId": 12346,
                "boardColumnId": {{doingColumnId}},
                "originalColumnName": "Missing column",
                "cardTypeId": {{systemCardTypeId}},
                "cardTypeName": "Story",
                "cardTypeEmoji": null,
                "title": "Archive me",
                "description": "Desc",
                "sortKey": "A",
                "tags": [],
                "tagNames": [],
                "createdAtUtc": "2026-04-26T12:00:00Z",
                "updatedAtUtc": "2026-04-26T12:00:00Z",
                "assignedUserId": null
              }
            }
            """;
        var archivedCard = await SeedArchivedCardV1Async(
            boardId,
            originalCardId: 12346,
            title: "Archive me",
            snapshotJson: snapshotJson);
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
        var todoColumnId = board.GetColumn("Todo").Id;
        var snapshotJson =
            $$"""
            {
              "schema": "archived-card",
              "version": 1,
              "capturedAtUtc": "2026-04-26T12:00:00Z",
              "payload": {
                "boardId": {{boardId}},
                "originalCardId": 12347,
                "boardColumnId": {{todoColumnId}},
                "originalColumnName": "Todo",
                "cardTypeId": {{customCardType.Id}},
                "cardTypeName": "Missing type",
                "cardTypeEmoji": null,
                "title": "Archive me",
                "description": "Desc",
                "sortKey": "A",
                "tags": [],
                "tagNames": [],
                "createdAtUtc": "2026-04-26T12:00:00Z",
                "updatedAtUtc": "2026-04-26T12:00:00Z",
                "assignedUserId": null
              }
            }
            """;
        var archivedCard = await SeedArchivedCardV1Async(
            boardId,
            originalCardId: 12347,
            title: "Archive me",
            snapshotJson: snapshotJson);

        var service = ResolveService<ICardArchiveService>();

        // Act
        var unarchiveResult = await service.UnarchiveCardAsync(boardId, archivedCard.OriginalCardId, ActorUserId);

        // Assert
        Assert.True(unarchiveResult.Success);
        Assert.NotNull(unarchiveResult.Data);
        Assert.Equal(systemCardTypeId, unarchiveResult.Data!.CardTypeId);
        Assert.NotEqual(customCardType.Id, unarchiveResult.Data.CardTypeId);
    }

    [Fact]
    public async Task UnarchiveCardAsync_WhenPortableReferencesDifferFromSnapshotIds_ShouldResolveByNameAndEmail()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Fallback")
            .AddColumn("Portable column")
            .Build();
        var boardId = board.BoardId;
        var portableCardType = DbContextForArrange.CardTypes.Add(new EntityCardType
        {
            BoardId = boardId,
            Name = "Portable type",
            Emoji = "📦",
            StyleName = "solid",
            StylePropertiesJson = "{}",
            IsSystem = false,
        }).Entity;
        var wrongCardType = DbContextForArrange.CardTypes.Add(new EntityCardType
        {
            BoardId = boardId,
            Name = "Wrong type",
            StyleName = "solid",
            StylePropertiesJson = "{}",
            IsSystem = false,
        }).Entity;
        var portableTag = DbContextForArrange.Tags.Add(new EntityTag
        {
            BoardId = boardId,
            Name = "Portable tag",
            NormalisedName = "PORTABLE TAG",
            StyleName = "solid",
            StylePropertiesJson = "{}",
        }).Entity;
        var wrongTag = DbContextForArrange.Tags.Add(new EntityTag
        {
            BoardId = boardId,
            Name = "Wrong tag",
            NormalisedName = "WRONG TAG",
            StyleName = "solid",
            StylePropertiesJson = "{}",
        }).Entity;
        var portableSlick = DbContextForArrange.Slicks.Add(new EntitySlick
        {
            BoardId = boardId,
            Name = "Portable slick",
            NormalisedName = "PORTABLE SLICK",
            StyleName = "solid",
            StylePropertiesJson = "{}",
        }).Entity;
        var wrongSlick = DbContextForArrange.Slicks.Add(new EntitySlick
        {
            BoardId = boardId,
            Name = "Wrong slick",
            NormalisedName = "WRONG SLICK",
            StyleName = "solid",
            StylePropertiesJson = "{}",
        }).Entity;
        await DbContextForArrange.SaveChangesAsync();
        var capturedAtUtc = new DateTime(2026, 4, 26, 12, 0, 0, DateTimeKind.Utc);
        var fallbackColumnId = board.GetColumn("Fallback").Id;
        var snapshotJson =
            $$"""
            {
              "schema": "archived-card",
              "version": 1,
              "capturedAtUtc": "2026-04-26T12:00:00Z",
              "payload": {
                "boardId": {{boardId}},
                "originalCardId": 12348,
                "boardColumnId": {{fallbackColumnId}},
                "originalColumnName": "portable COLUMN",
                "cardTypeId": {{wrongCardType.Id}},
                "cardTypeName": "portable TYPE",
                "cardTypeEmoji": null,
                "title": "Portable restore",
                "description": "Desc",
                "sortKey": "SNAPSHOT-SORT-KEY",
                "tags": [
                  {
                    "id": {{wrongTag.Id}},
                    "name": "Wrong tag",
                    "styleName": "solid",
                    "stylePropertiesJson": "{}",
                    "emoji": null
                  }
                ],
                "tagNames": ["portable TAG"],
                "createdAtUtc": "2026-04-26T12:00:00Z",
                "updatedAtUtc": "2026-04-26T12:00:00Z",
                "assignedUserId": null,
                "comments": [
                  {
                    "text": "Portable author",
                    "createdAtUtc": "2026-04-26T12:00:00Z",
                    "authorUserId": 999999,
                    "authorEmail": "ACTOR@LOCALHOST"
                  }
                ],
                "slickId": {{wrongSlick.Id}},
                "slickName": "portable SLICK"
              }
            }
            """;
        var archivedCard = await SeedArchivedCardV1Async(
            boardId,
            originalCardId: 12348,
            title: "Portable restore",
            snapshotJson: snapshotJson,
            archivedAtUtc: capturedAtUtc);
        var service = ResolveService<ICardArchiveService>();

        // Act
        var result = await service.UnarchiveCardAsync(boardId, archivedCard.OriginalCardId, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(board.GetColumn("Portable column").Id, result.Data!.BoardColumnId);
        Assert.Equal(portableCardType.Id, result.Data.CardTypeId);
        Assert.Equal(portableSlick.Id, result.Data.SlickId);
        Assert.NotEqual("SNAPSHOT-SORT-KEY", result.Data.SortKey);
        var restoredCard = await DbContextForAssert.Cards
            .Include(x => x.CardTags)
            .ThenInclude(x => x.Tag)
            .Include(x => x.Comments)
            .SingleAsync(x => x.BoardId == boardId && x.BoardCardId == archivedCard.OriginalCardId);
        Assert.Equal(portableTag.Id, Assert.Single(restoredCard.CardTags).TagId);
        Assert.Equal(ActorUserId, Assert.Single(restoredCard.Comments).AuthorUserId);
    }

    [Fact]
    public async Task UnarchiveCardAsync_WhenPortableNamesAreAbsent_ShouldNotBindTagSlickOrAuthorIds()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var wrongTag = DbContextForArrange.Tags.Add(new EntityTag
        {
            BoardId = board.BoardId,
            Name = "Wrong tag",
            NormalisedName = "WRONG TAG",
            StyleName = "solid",
            StylePropertiesJson = "{}",
        }).Entity;
        var wrongSlick = DbContextForArrange.Slicks.Add(new EntitySlick
        {
            BoardId = board.BoardId,
            Name = "Wrong slick",
            NormalisedName = "WRONG SLICK",
            StyleName = "solid",
            StylePropertiesJson = "{}",
        }).Entity;
        await DbContextForArrange.SaveChangesAsync();
        var capturedAtUtc = new DateTime(2026, 4, 26, 12, 0, 0, DateTimeKind.Utc);
        var todoColumnId = board.GetColumn("Todo").Id;
        var systemCardTypeId = await GetSystemCardTypeIdForBoardAsync(board.BoardId);
        var snapshotJson =
            $$"""
            {
              "schema": "archived-card",
              "version": 1,
              "capturedAtUtc": "2026-04-26T12:00:00Z",
              "payload": {
                "boardId": {{board.BoardId}},
                "originalCardId": 12349,
                "boardColumnId": {{todoColumnId}},
                "originalColumnName": "Todo",
                "cardTypeId": {{systemCardTypeId}},
                "cardTypeName": "Story",
                "cardTypeEmoji": null,
                "title": "No portable references",
                "description": "Desc",
                "sortKey": "A",
                "tags": [
                  {
                    "id": {{wrongTag.Id}},
                    "name": "Wrong tag",
                    "styleName": "solid",
                    "stylePropertiesJson": "{}",
                    "emoji": null
                  }
                ],
                "tagNames": [],
                "createdAtUtc": "2026-04-26T12:00:00Z",
                "updatedAtUtc": "2026-04-26T12:00:00Z",
                "assignedUserId": null,
                "comments": [
                  {
                    "text": "ID-only author",
                    "createdAtUtc": "2026-04-26T12:00:00Z",
                    "authorUserId": {{ActorUserId}},
                    "authorEmail": null
                  }
                ],
                "slickId": {{wrongSlick.Id}},
                "slickName": null
              }
            }
            """;
        var archivedCard = await SeedArchivedCardV1Async(
            board.BoardId,
            originalCardId: 12349,
            title: "No portable references",
            snapshotJson: snapshotJson,
            archivedAtUtc: capturedAtUtc);
        var service = ResolveService<ICardArchiveService>();

        // Act
        var result = await service.UnarchiveCardAsync(board.BoardId, archivedCard.OriginalCardId, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data!.Tags);
        Assert.Null(result.Data.SlickId);
        var restoredCard = await DbContextForAssert.Cards
            .Include(x => x.CardTags)
            .Include(x => x.Comments)
            .SingleAsync(x => x.BoardId == board.BoardId && x.BoardCardId == archivedCard.OriginalCardId);
        Assert.Empty(restoredCard.CardTags);
        Assert.Null(Assert.Single(restoredCard.Comments).AuthorUserId);
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
        string title,
        string snapshotJson,
        DateTime? archivedAtUtc = null)
    {
        var storedArchivedAtUtc = archivedAtUtc ?? new DateTime(2026, 4, 26, 12, 0, 0, DateTimeKind.Utc);
        var archivedCard = DbContextForArrange.Set<ArchivedCardEntity>().Add(new ArchivedCardEntity
        {
            BoardId = boardId,
            OriginalCardId = originalCardId,
            ArchivedAtUtc = storedArchivedAtUtc,
            SnapshotJson = snapshotJson,
            SearchTitle = title,
            SearchTagsJson = "[]",
            SearchTextNormalised = title.ToUpperInvariant()
        }).Entity;
        await DbContextForArrange.SaveChangesAsync();
        return archivedCard;
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
