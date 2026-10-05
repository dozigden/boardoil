using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using BoardOil.Contracts.CardField;
using BoardOil.Services.CardField;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Card;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Board;
using BoardOil.Services.Card;
using BoardOil.Services.Style;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class BoardImportServiceTests : TestBaseDb
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ImportBoardPackageAsync_WithSystemComments_ShouldPreserveMarkerWithoutAnAuthor(int schemaVersion)
    {
        const string boardJson = """
            {"name":"System comments","cardTypes":[{"name":"Story","isSystem":true}],"tags":[],
             "columns":[{"title":"Todo","cards":[{"id":1,"title":"Card","description":"","cardTypeName":"Story","tagNames":[],
               "comments":[{"text":"Estimate: 7","postedAtUtc":"2026-05-01T08:00:00Z","isSystem":true}]}]}]}
            """;
        var manifest = BoardPackageContract.CreateManifest("test") with { SchemaVersion = schemaVersion };

        var result = await ResolveService<IBoardPackageImportService>().ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackageWithRawBoardPayload(manifest, boardJson)), ActorUserId);

        Assert.True(result.Success, result.Message);
        var comment = await DbContextForAssert.CardComments.SingleAsync();
        Assert.True(comment.IsSystem);
        Assert.Null(comment.AuthorUserId);
        Assert.Equal("Estimate: 7", comment.Text);
        Assert.Equal(new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc), comment.PostedAtUtc);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ImportBoardPackageAsync_WhenCommentsLackEditMetadata_ShouldImportAsUnedited(int schemaVersion)
    {
        var manifest = BoardPackageContract.CreateManifest("legacy") with { SchemaVersion = schemaVersion };
        const string boardJson = """
            {"name":"Legacy comments","cardTypes":[{"name":"Story","isSystem":true}],"tags":[],
             "columns":[{"title":"Todo","cards":[{"id":1,"title":"Card","description":"","cardTypeName":"Story","tagNames":[],
               "comments":[{"text":"Original comment","postedAtUtc":"2026-05-01T08:00:00Z"}]}]}]}
            """;

        var result = await ResolveService<IBoardPackageImportService>().ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackageWithRawBoardPayload(manifest, boardJson)), ActorUserId);

        Assert.True(result.Success, result.Message);
        var comment = await DbContextForAssert.CardComments.SingleAsync();
        Assert.Equal("Original comment", comment.Text);
        Assert.Equal(new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc), comment.PostedAtUtc);
        Assert.Null(comment.EditedAtUtc);
        Assert.False(comment.IsSystem);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ImportBoardPackageAsync_ShouldCalculateChecklistCountsFromDescription(int schemaVersion)
    {
        var manifest = BoardPackageContract.CreateManifest("0.3.0") with { SchemaVersion = schemaVersion };
        var payload = new BoardPackageBoardDto("Checklist import", null,
            [new BoardPackageCardTypeDto("Story", null, true)], [],
            [new BoardPackageColumnDto("Todo",
                [new BoardPackageCardDto("Checklist", "- [x] Done\n- [ ] Open", "Story", [], Id: 1)])]);

        var result = await ResolveService<IBoardPackageImportService>().ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackage(manifest, payload)), ActorUserId);

        Assert.True(result.Success, result.Message);
        var returned = Assert.Single(Assert.Single(result.Data!.Columns).Cards);
        Assert.Equal(1, returned.CompletedChecklistItemCount);
        Assert.Equal(2, returned.TotalChecklistItemCount);
        var stored = await DbContextForAssert.Cards.SingleAsync(x => x.BoardId == result.Data.Id);
        Assert.Equal(1, stored.CompletedChecklistItemCount);
        Assert.Equal(2, stored.TotalChecklistItemCount);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ImportBoardPackageAsync_ShouldCreateBoardWithImportedColumnsCardsTagsAndCardTypes()
    {
        var manifest = BoardPackageContract.CreateManifest("0.3.0");
        var cardCreatedUtc = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc);
        var cardUpdatedUtc = cardCreatedUtc.AddDays(2);
        var payload = new BoardPackageBoardDto(
            "Imported Package Board",
            "Imported package description",
            [
                new BoardPackageCardTypeDto("Story", null, true, "solid", """{"backgroundColor":"#FFFFFF","textColorMode":"auto"}"""),
                new BoardPackageCardTypeDto("Bug", "🐞", false, "gradient", """{"leftColor":"#F6D32D","rightColor":"#C64600","textColorMode":"auto"}""")
            ],
            [
                new BoardPackageTagDto("Urgent", "solid", """{"backgroundColor":"#ED333B","textColorMode":"auto"}""", "🟥")
            ],
            [
                new BoardPackageColumnDto(
                    "Todo",
                    [
                        new BoardPackageCardDto(
                            "Fix login",
                            "Investigate and fix",
                            "Bug",
                            ["Urgent", "NeedsReview"],
                            SlickName: "Release train",
                            CardCreatedUtc: cardCreatedUtc,
                            CardUpdatedUtc: cardUpdatedUtc)
                    ]),
                new BoardPackageColumnDto(
                    "Done",
                    [
                        new BoardPackageCardDto("Ship release", "Already done", "Story", [])
                    ])
            ],
            [
                new BoardPackageSlickDto("Release train", "solid", """{"backgroundColor":"#2E8B57","textColorMode":"auto"}""")
            ],
            SlickCohesionModeEnabled: false,
            CardAttachmentThumbnailsEnabled: false);

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackage(manifest, payload)),
            ActorUserId);

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("Imported Package Board", result.Data!.Name);
        Assert.Equal("Imported package description", result.Data.Description);
        Assert.False(result.Data.SlickCohesionModeEnabled);
        Assert.False(result.Data.CardAttachmentThumbnailsEnabled);
        Assert.Equal(["Todo", "Done"], result.Data.Columns.Select(x => x.Title).ToArray());
        Assert.Equal("Bug", result.Data.Columns[0].Cards[0].CardTypeName);
        Assert.Equal(["NeedsReview", "Urgent"], result.Data.Columns[0].Cards[0].TagNames);

        var boardId = result.Data.Id;
        var ownerMembership = DbContextForAssert.BoardMembers.Single(x => x.BoardId == boardId && x.UserId == ActorUserId);
        Assert.Equal(BoardMemberRole.Owner, ownerMembership.Role);
        var importedBoard = DbContextForAssert.Boards.Single(x => x.Id == boardId);
        Assert.False(importedBoard.SlickCohesionModeEnabled);
        Assert.False(importedBoard.CardAttachmentThumbnailsEnabled);

        var cardTypes = DbContextForAssert.CardTypes.Where(x => x.BoardId == boardId).OrderBy(x => x.Name).ToList();
        Assert.Equal(["Bug", "Story"], cardTypes.Select(x => x.Name).ToArray());
        Assert.Contains(
            cardTypes,
            x => x.Name == "Story"
                && x.IsSystem
                && x.StyleName == "solid");
        Assert.Contains(
            cardTypes,
            x => x.Name == "Bug"
                && !x.IsSystem
                && x.Emoji == "🐞"
                && x.StyleName == "gradient"
                && x.StylePropertiesJson == """{"leftColor":"#F6D32D","rightColor":"#C64600","textColorMode":"auto","borderMode":"auto"}""");

        var tags = DbContextForAssert.Tags.Where(x => x.BoardId == boardId).OrderBy(x => x.Name).ToList();
        Assert.Equal(["NeedsReview", "Urgent"], tags.Select(x => x.Name).ToArray());
        Assert.Contains(tags, x => x.Name == "Urgent" && x.StyleName == "solid" && x.Emoji == "🟥");
        Assert.Contains(tags, x => x.Name == "NeedsReview" && x.StyleName == StyleDefinitionCodec.PresetsStyleName);

        var slicks = DbContextForAssert.Slicks.Where(x => x.BoardId == boardId).ToList();
        var releaseTrainSlick = Assert.Single(slicks);
        Assert.Equal("Release train", releaseTrainSlick.Name);
        Assert.Equal("RELEASE TRAIN", releaseTrainSlick.NormalisedName);
        Assert.Equal("solid", releaseTrainSlick.StyleName);
        Assert.Equal("""{"backgroundColor":"#2E8B57","textColorMode":"auto","borderMode":"auto"}""", releaseTrainSlick.StylePropertiesJson);

        var importedCard = DbContextForAssert.Cards.Single(x => x.BoardColumn.BoardId == boardId && x.Title == "Fix login");
        Assert.Equal(releaseTrainSlick.Id, importedCard.SlickId);
        Assert.Equal(cardCreatedUtc, importedCard.CardCreatedUtc);
        Assert.Equal(cardUpdatedUtc, importedCard.CardUpdatedUtc);
        var legacyImportedCard = DbContextForAssert.Cards.Single(x => x.BoardColumn.BoardId == boardId && x.Title == "Ship release");
        Assert.NotEqual(default, legacyImportedCard.CardCreatedUtc);
        Assert.Equal(legacyImportedCard.CardCreatedUtc, legacyImportedCard.CardUpdatedUtc);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ImportBoardPackageAsync_WhenCardAttachmentThumbnailsSettingIsPresent_ShouldPreserveIt(
        int schemaVersion)
    {
        var manifest = BoardPackageContract.CreateManifest("0.3.0") with { SchemaVersion = schemaVersion };
        var payload = new BoardPackageBoardDto(
            "Thumbnail Setting Board",
            null,
            [new BoardPackageCardTypeDto("Story", null, true)],
            [],
            [new BoardPackageColumnDto("Todo", [])],
            CardAttachmentThumbnailsEnabled: false);
        var service = ResolveService<IBoardPackageImportService>();

        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackage(manifest, payload)),
            ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.False(result.Data!.CardAttachmentThumbnailsEnabled);
        Assert.False(DbContextForAssert.Boards
            .Single(x => x.Id == result.Data.Id)
            .CardAttachmentThumbnailsEnabled);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ImportBoardPackageAsync_WhenLogicalCardTimestampsArePresent_ShouldPreserveThem(int schemaVersion)
    {
        var cardCreatedUtc = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc);
        var cardUpdatedUtc = cardCreatedUtc.AddDays(2);
        var manifest = BoardPackageContract.CreateManifest("0.3.0") with { SchemaVersion = schemaVersion };
        var payload = new BoardPackageBoardDto(
            "Timestamp Board",
            null,
            [new BoardPackageCardTypeDto("Story", null, true)],
            [],
            [
                new BoardPackageColumnDto(
                    "Todo",
                    [
                        new BoardPackageCardDto(
                            "Timestamped card",
                            "Description",
                            "Story",
                            [],
                            CardCreatedUtc: cardCreatedUtc,
                            CardUpdatedUtc: cardUpdatedUtc)
                    ])
            ]);

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackage(manifest, payload)),
            ActorUserId);

        Assert.True(result.Success);
        var importedCard = await DbContextForAssert.Cards.SingleAsync(x => x.BoardId == result.Data!.Id);
        Assert.Equal(cardCreatedUtc, importedCard.CardCreatedUtc);
        Assert.Equal(cardUpdatedUtc, importedCard.CardUpdatedUtc);
    }

    [Fact]
    public async Task ImportBoardPackageAsync_WhenCardIncludesComments_ShouldImportAndMapAuthorsByEmailBestEffort()
    {
        var actor = DbContextForArrange.Users.Single(x => x.Id == ActorUserId);
        var manifest = BoardPackageContract.CreateManifest("0.3.0");
        var payload = new BoardPackageBoardDto(
            "Comment Import Board",
            "Comment import board description",
            [new BoardPackageCardTypeDto("Story", null, true)],
            [],
            [
                new BoardPackageColumnDto(
                    "Todo",
                    [
                        new BoardPackageCardDto(
                            "Card with comments",
                            "Description",
                            "Story",
                            [],
                            null,
                            [
                                new BoardPackageCommentDto(
                                    "  First imported comment  ",
                                    new DateTime(2026, 05, 02, 8, 0, 0, DateTimeKind.Utc),
                                    actor.Email.ToUpperInvariant()),
                                new BoardPackageCommentDto(
                                    "Second imported comment",
                                    new DateTime(2026, 05, 02, 8, 1, 0, DateTimeKind.Utc),
                                    "missing-user@example.com")
                            ])
                    ])
            ]);

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackage(manifest, payload)),
            ActorUserId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var boardId = result.Data!.Id;
        var importedCard = DbContextForAssert.Cards.Single(x => x.BoardColumn.BoardId == boardId);
        var importedComments = DbContextForAssert.CardComments
            .Where(x => x.CardId == importedCard.Id)
            .OrderBy(x => x.PostedAtUtc)
            .ToList();

        Assert.Equal(2, importedComments.Count);
        Assert.Equal("First imported comment", importedComments[0].Text);
        Assert.Equal(actor.Id, importedComments[0].AuthorUserId);
        Assert.Equal("Second imported comment", importedComments[1].Text);
        Assert.Null(importedComments[1].AuthorUserId);
    }

    [Fact]
    public async Task ImportBoardPackageAsync_WhenAssignedUserEmailMatchesActiveUser_ShouldAssignCard()
    {
        var actor = DbContextForArrange.Users.Single(x => x.Id == ActorUserId);
        var manifest = BoardPackageContract.CreateManifest("0.3.0");
        var payload = new BoardPackageBoardDto(
            "Assigned User Import Board",
            "Assigned user import board description",
            [new BoardPackageCardTypeDto("Story", null, true)],
            [],
            [
                new BoardPackageColumnDto(
                    "Todo",
                    [
                        new BoardPackageCardDto("Assigned card", "Description", "Story", [], actor.Email.ToUpperInvariant())
                    ])
            ]);

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackage(manifest, payload)),
            ActorUserId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var boardId = result.Data!.Id;
        var importedCard = DbContextForAssert.Cards.Single(x => x.BoardColumn.BoardId == boardId);
        Assert.Equal(actor.Id, importedCard.AssignedUserId);
    }

    [Fact]
    public async Task ImportBoardPackageAsync_WhenAssignedUserEmailMatchesActiveClientIdentity_ShouldAssignCard()
    {
        var now = DateTime.UtcNow;
        var clientEmail = $"client-{Guid.NewGuid():N}@example.com";
        var clientUser = new EntityUser
        {
            UserName = $"client-{Guid.NewGuid():N}",
            Email = clientEmail,
            NormalisedEmail = clientEmail.ToLowerInvariant(),
            PasswordHash = "test-hash",
            Role = UserRole.Standard,
            IdentityType = UserIdentityType.Client,
            IsActive = true,
        };
        DbContextForArrange.Users.Add(clientUser);
        await DbContextForArrange.SaveChangesAsync();

        var manifest = BoardPackageContract.CreateManifest("0.3.0");
        var payload = new BoardPackageBoardDto(
            "Client Identity Import Board",
            "Client identity import board description",
            [new BoardPackageCardTypeDto("Story", null, true)],
            [],
            [
                new BoardPackageColumnDto(
                    "Todo",
                    [
                        new BoardPackageCardDto("Assigned card", "Description", "Story", [], clientUser.Email)
                    ])
            ]);

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackage(manifest, payload)),
            ActorUserId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var boardId = result.Data!.Id;
        var importedCard = DbContextForAssert.Cards.Single(x => x.BoardColumn.BoardId == boardId);
        Assert.Equal(clientUser.Id, importedCard.AssignedUserId);
    }

    [Fact]
    public async Task ImportBoardPackageAsync_WhenAssignedUserEmailMatchesInactiveUser_ShouldLeaveCardUnassigned()
    {
        var now = DateTime.UtcNow;
        var inactiveUserEmail = $"inactive-{Guid.NewGuid():N}@example.com";
        var inactiveUser = new EntityUser
        {
            UserName = $"inactive-{Guid.NewGuid():N}",
            Email = inactiveUserEmail,
            NormalisedEmail = inactiveUserEmail.ToLowerInvariant(),
            PasswordHash = "test-hash",
            Role = UserRole.Standard,
            IdentityType = UserIdentityType.User,
            IsActive = false,
        };
        DbContextForArrange.Users.Add(inactiveUser);
        await DbContextForArrange.SaveChangesAsync();

        var manifest = BoardPackageContract.CreateManifest("0.3.0");
        var payload = new BoardPackageBoardDto(
            "Inactive User Import Board",
            "Inactive user import board description",
            [new BoardPackageCardTypeDto("Story", null, true)],
            [],
            [
                new BoardPackageColumnDto(
                    "Todo",
                    [
                        new BoardPackageCardDto("Unassigned card", "Description", "Story", [], inactiveUser.Email)
                    ])
            ]);

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackage(manifest, payload)),
            ActorUserId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var boardId = result.Data!.Id;
        var importedCard = DbContextForAssert.Cards.Single(x => x.BoardColumn.BoardId == boardId);
        Assert.Null(importedCard.AssignedUserId);
    }

    [Fact]
    public async Task ImportBoardPackageAsync_WhenAssignedUserEmailIsUnknown_ShouldLeaveCardUnassigned()
    {
        var manifest = BoardPackageContract.CreateManifest("0.3.0");
        var payload = new BoardPackageBoardDto(
            "Unknown User Import Board",
            "Unknown user import board description",
            [new BoardPackageCardTypeDto("Story", null, true)],
            [],
            [
                new BoardPackageColumnDto(
                    "Todo",
                    [
                        new BoardPackageCardDto("Unassigned card", "Description", "Story", [], "missing-user@example.com")
                    ])
            ]);

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackage(manifest, payload)),
            ActorUserId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var boardId = result.Data!.Id;
        var importedCard = DbContextForAssert.Cards.Single(x => x.BoardColumn.BoardId == boardId);
        Assert.Null(importedCard.AssignedUserId);
    }

    [Fact]
    public async Task ImportBoardPackageAsync_WhenAssignedUserEmailIsInvalid_ShouldLeaveCardUnassigned()
    {
        var manifest = BoardPackageContract.CreateManifest("0.3.0");
        var payload = new BoardPackageBoardDto(
            "Invalid Email Import Board",
            "Invalid email import board description",
            [new BoardPackageCardTypeDto("Story", null, true)],
            [],
            [
                new BoardPackageColumnDto(
                    "Todo",
                    [
                        new BoardPackageCardDto("Unassigned card", "Description", "Story", [], "invalid-email")
                    ])
            ]);

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackage(manifest, payload)),
            ActorUserId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var boardId = result.Data!.Id;
        var importedCard = DbContextForAssert.Cards.Single(x => x.BoardColumn.BoardId == boardId);
        Assert.Null(importedCard.AssignedUserId);
    }

    [Fact]
    public async Task ImportBoardPackageAsync_WhenAssignedUserEmailIsMissing_ShouldLeaveCardUnassigned()
    {
        var manifest = BoardPackageContract.CreateManifest("0.3.0");
        var payload = new BoardPackageBoardDto(
            "Missing Assignment Field Board",
            "Missing assignment field board description",
            [new BoardPackageCardTypeDto("Story", null, true)],
            [],
            [
                new BoardPackageColumnDto(
                    "Todo",
                    [
                        new BoardPackageCardDto("Unassigned card", "Description", "Story", [])
                    ])
            ]);

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackage(manifest, payload)),
            ActorUserId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var boardId = result.Data!.Id;
        var importedCard = DbContextForAssert.Cards.Single(x => x.BoardColumn.BoardId == boardId);
        Assert.Null(importedCard.AssignedUserId);
    }

    [Fact]
    public async Task ImportBoardPackageAsync_WithArchivePayload_ShouldImportArchivedCards()
    {
        var manifest = BoardPackageContract.CreateManifest("0.3.0");
        var payload = new BoardPackageBoardDto(
            "Archive Import Board",
            "Archive import board description",
            [new BoardPackageCardTypeDto("Story", null, true)],
            [],
            []);
        var archivePayload = new BoardPackageArchiveDto(
            [
                new BoardPackageArchivedCardDto(
                    12345,
                    "Imported archived card",
                    ["Urgent"],
                    new DateTime(2026, 04, 20, 10, 0, 0, DateTimeKind.Utc),
                    """{"schema":"archived-card","version":1,"capturedAtUtc":"2026-04-20T10:00:00Z","payload":{"title":"Imported archived card"}}""")
            ]);

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackage(manifest, payload, archivePayload)),
            ActorUserId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var boardId = result.Data!.Id;
        var archivedCard = DbContextForAssert.ArchivedCards.Single(x => x.BoardId == boardId);
        Assert.Equal(12345, archivedCard.OriginalCardId);
        Assert.Equal("Imported archived card", archivedCard.SearchTitle);
        Assert.Equal("""["Urgent"]""", archivedCard.SearchTagsJson);
        Assert.Contains("IMPORTED ARCHIVED CARD", archivedCard.SearchTextNormalised);
        Assert.Contains("URGENT", archivedCard.SearchTextNormalised);
        Assert.Contains("\"schema\":\"archived-card\"", archivedCard.SnapshotJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportBoardPackageAsync_WithArchivedAssignedCard_ShouldPreserveSnapshotAndRestoreAssigneeByEmail()
    {
        // Arrange
        var sourceBoard = CreateBoard("Source Board")
            .AddColumn("Todo")
            .AddCard("Assigned archive", "Description")
            .Build();
        var sourceCard = sourceBoard.GetCard("Todo", "Assigned archive");
        sourceCard.AssignedUserId = ActorUserId;
        await DbContextForArrange.SaveChangesAsync();
        var archiveService = ResolveService<ICardArchiveService>();
        var archiveResult = await archiveService.ArchiveCardAsync(
            sourceBoard.BoardId,
            sourceCard.RequireBoardCardId(),
            ActorUserId);
        Assert.True(archiveResult.Success);
        Assert.NotNull(archiveResult.Data);
        var exportedSnapshotJson = archiveResult.Data!.SnapshotJson;
        var exportService = ResolveService<IBoardExportService>();
        var exportResult = await exportService.ExportBoardAsync(sourceBoard.BoardId, ActorUserId, "0.4.0");
        Assert.True(exportResult.Success);
        Assert.NotNull(exportResult.Data);

        var importService = ResolveService<IBoardPackageImportService>();
        var importResult = await importService.ImportBoardPackageAsync(
            new ImportBoardPackageRequest("Imported Board", exportResult.Data!.Content),
            ActorUserId);
        Assert.True(importResult.Success);
        Assert.NotNull(importResult.Data);
        var importedBoardId = importResult.Data!.Id;
        var importedArchivedCard = await DbContextForAssert.ArchivedCards
            .SingleAsync(x => x.BoardId == importedBoardId);
        Assert.Equal(exportedSnapshotJson, importedArchivedCard.SnapshotJson);

        // Act
        var unarchiveResult = await archiveService.UnarchiveCardAsync(
            importedBoardId,
            importedArchivedCard.OriginalCardId,
            ActorUserId);

        // Assert
        Assert.True(unarchiveResult.Success);
        Assert.NotNull(unarchiveResult.Data);
        Assert.Equal(ActorUserId, unarchiveResult.Data!.AssignedUserId);
    }

    [Fact]
    public async Task ImportBoardPackageAsync_WithArchivedPortableReferences_ShouldHydrateAndRestoreDestinationEntities()
    {
        // Arrange
        var sourceBoard = CreateBoard("Source Board")
            .AddColumn("Fallback")
            .AddColumn("Portable column")
            .AddCard("Portable archive", "Description")
            .Build();
        var sourceBoardId = sourceBoard.BoardId;
        var sourceCard = sourceBoard.GetCard("Portable column", "Portable archive");
        var sourceCardType = DbContextForArrange.CardTypes.Add(new EntityCardType
        {
            BoardId = sourceBoardId,
            Name = "Portable type",
            Emoji = "📦",
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#112233","textColorMode":"auto","borderMode":"auto"}""",
            IsSystem = false,
        }).Entity;
        var sourceTag = DbContextForArrange.Tags.Add(new EntityTag
        {
            BoardId = sourceBoardId,
            Name = "Portable tag",
            NormalisedName = "PORTABLE TAG",
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#112233","textColorMode":"auto","borderMode":"auto"}""",
        }).Entity;
        var sourceSlick = DbContextForArrange.Slicks.Add(new EntitySlick
        {
            BoardId = sourceBoardId,
            Name = "Portable slick",
            NormalisedName = "PORTABLE SLICK",
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#112233","textColorMode":"auto","borderMode":"auto"}""",
        }).Entity;
        await DbContextForArrange.SaveChangesAsync();
        var cardService = ResolveService<CardService>();
        var updateResult = await cardService.UpdateCardAsync(
            sourceBoardId,
            sourceCard.RequireBoardCardId(),
            new UpdateCardRequest(
                "Portable archive",
                "Description",
                [sourceTag.Name],
                sourceCardType.Id,
                SlickName: sourceSlick.Name),
            ActorUserId);
        Assert.True(updateResult.Success);
        DbContextForArrange.CardComments.Add(new EntityCardComment
        {
            CardId = sourceCard.Id,
            AuthorUserId = ActorUserId,
            Text = "Portable author",
            PostedAtUtc = new DateTime(2026, 8, 25, 8, 0, 0, DateTimeKind.Utc),
        });
        await DbContextForArrange.SaveChangesAsync();
        var archiveService = ResolveService<ICardArchiveService>();
        var archiveResult = await archiveService.ArchiveCardAsync(
            sourceBoardId,
            sourceCard.RequireBoardCardId(),
            ActorUserId);
        Assert.True(archiveResult.Success);
        Assert.NotNull(archiveResult.Data);
        var exportedSnapshotJson = archiveResult.Data!.SnapshotJson;
        var exportService = ResolveService<IBoardExportService>();
        var exportResult = await exportService.ExportBoardAsync(sourceBoardId, ActorUserId, "0.4.0");
        Assert.True(exportResult.Success);
        Assert.NotNull(exportResult.Data);
        var importService = ResolveService<IBoardPackageImportService>();
        var importResult = await importService.ImportBoardPackageAsync(
            new ImportBoardPackageRequest("Imported Board", exportResult.Data!.Content),
            ActorUserId);
        Assert.True(importResult.Success);
        Assert.NotNull(importResult.Data);
        var importedBoardId = importResult.Data!.Id;
        var importedColumn = await DbContextForAssert.Columns
            .SingleAsync(x => x.BoardId == importedBoardId && x.Title == "Portable column");
        var importedCardType = await DbContextForAssert.CardTypes
            .SingleAsync(x => x.BoardId == importedBoardId && x.Name == "Portable type");
        var importedTag = await DbContextForAssert.Tags
            .SingleAsync(x => x.BoardId == importedBoardId && x.Name == "Portable tag");
        var importedSlick = await DbContextForAssert.Slicks
            .SingleAsync(x => x.BoardId == importedBoardId && x.Name == "Portable slick");
        var importedArchivedCard = await DbContextForAssert.ArchivedCards
            .SingleAsync(x => x.BoardId == importedBoardId);
        Assert.Equal(exportedSnapshotJson, importedArchivedCard.SnapshotJson);
        Assert.NotEqual(sourceBoard.GetColumn("Portable column").Id, importedColumn.Id);
        Assert.NotEqual(sourceCardType.Id, importedCardType.Id);
        Assert.NotEqual(sourceTag.Id, importedTag.Id);
        Assert.NotEqual(sourceSlick.Id, importedSlick.Id);

        // Act
        var detailResult = await archiveService.GetArchivedCardAsync(
            importedBoardId,
            importedArchivedCard.OriginalCardId,
            ActorUserId);
        var unarchiveResult = await archiveService.UnarchiveCardAsync(
            importedBoardId,
            importedArchivedCard.OriginalCardId,
            ActorUserId);

        // Assert
        Assert.True(detailResult.Success);
        Assert.NotNull(detailResult.Data);
        Assert.Equal(importedColumn.Id, detailResult.Data!.Card.BoardColumnId);
        Assert.Equal(importedCardType.Id, detailResult.Data.Card.CardTypeId);
        Assert.Equal(importedTag.Id, Assert.Single(detailResult.Data.Card.Tags).Id);
        Assert.Equal(importedSlick.Id, detailResult.Data.Card.SlickId);
        Assert.True(unarchiveResult.Success);
        Assert.NotNull(unarchiveResult.Data);
        Assert.Equal(importedColumn.Id, unarchiveResult.Data!.BoardColumnId);
        Assert.Equal(importedCardType.Id, unarchiveResult.Data.CardTypeId);
        Assert.Equal(importedSlick.Id, unarchiveResult.Data.SlickId);
        var restoredCard = await DbContextForAssert.Cards
            .Include(x => x.CardTags)
            .Include(x => x.Comments)
            .SingleAsync(x => x.BoardId == importedBoardId && x.BoardCardId == importedArchivedCard.OriginalCardId);
        Assert.Equal(importedTag.Id, Assert.Single(restoredCard.CardTags).TagId);
        Assert.Equal(ActorUserId, Assert.Single(restoredCard.Comments).AuthorUserId);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ImportBoardPackageAsync_WhenLegacyPackageIdsConflict_ShouldAllocateUniqueBoardScopedIds(int schemaVersion)
    {
        var manifest = BoardPackageContract.CreateManifest("0.3.0") with { SchemaVersion = schemaVersion };
        var payload = new BoardPackageBoardDto(
            "Legacy Identity Board",
            "Legacy identity board description",
            [new BoardPackageCardTypeDto("Story", null, true)],
            [],
            [
                new BoardPackageColumnDto(
                    "Todo",
                    [
                        new BoardPackageCardDto("Active A", "Description", "Story", []),
                        new BoardPackageCardDto("Active B", "Description", "Story", [])
                    ])
            ]);
        var archivedAtUtc = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
        var archivePayload = new BoardPackageArchiveDto(
            [
                new BoardPackageArchivedCardDto(1, "Preserved archive", [], archivedAtUtc, "{}"),
                new BoardPackageArchivedCardDto(1, "Duplicate archive", [], archivedAtUtc.AddMinutes(1), "{}"),
                new BoardPackageArchivedCardDto(0, "Invalid archive", [], archivedAtUtc.AddMinutes(2), "{}")
            ]);

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackage(manifest, payload, archivePayload)),
            ActorUserId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var boardId = result.Data!.Id;
        var activeCards = DbContextForAssert.Cards
            .Where(x => x.BoardId == boardId)
            .OrderBy(x => x.Title)
            .ToList();
        var archivedCards = DbContextForAssert.ArchivedCards
            .Where(x => x.BoardId == boardId)
            .ToList();
        Assert.Equal([2, 3], activeCards.Select(x => x.BoardCardId).ToArray());
        Assert.Contains(archivedCards, x => x.SearchTitle == "Preserved archive" && x.OriginalCardId == 1);
        Assert.Contains(archivedCards, x => x.SearchTitle == "Duplicate archive" && x.OriginalCardId == 4);
        Assert.Contains(archivedCards, x => x.SearchTitle == "Invalid archive" && x.OriginalCardId == 5);
        Assert.Equal(5, activeCards.Count + archivedCards.Count);
        Assert.Equal(
            5,
            activeCards.Select(x => x.BoardCardId)
                .Concat(archivedCards.Select(x => x.OriginalCardId))
                .Distinct()
                .Count());
        var nextCardId = DbContextForAssert.BoardCardIdSequences
            .Where(x => x.BoardId == boardId)
            .Select(x => x.NextCardId)
            .Single();
        Assert.Equal(6, nextCardId);
    }

    [Fact]
    public async Task ImportBoardPackageAsync_WhenDeletedHighWaterMarkIsNotExported_ShouldCalculateNextCardIdFromPresentIds()
    {
        var sourceBoard = CreateBoard("Round Trip Board")
            .AddColumn("Todo")
            .AddCard("Keep active", "Description")
            .AddCard("Keep archived", "Description")
            .AddCard("Hard deleted", "Description")
            .Build();
        var activeCardId = sourceBoard.GetCard("Todo", "Keep active").RequireBoardCardId();
        var archivedCardId = sourceBoard.GetCard("Todo", "Keep archived").RequireBoardCardId();
        var deletedCardId = sourceBoard.GetCard("Todo", "Hard deleted").RequireBoardCardId();
        var archiveService = ResolveService<ICardArchiveService>();
        var archiveResult = await archiveService.ArchiveCardAsync(sourceBoard.BoardId, archivedCardId, ActorUserId);
        Assert.True(archiveResult.Success);
        var cardService = ResolveService<ICardService>();
        var deleteResult = await cardService.DeleteCardAsync(sourceBoard.BoardId, deletedCardId, ActorUserId);
        Assert.True(deleteResult.Success);
        var exportService = ResolveService<IBoardExportService>();
        var exportResult = await exportService.ExportBoardAsync(sourceBoard.BoardId, ActorUserId, "0.4.0");
        Assert.True(exportResult.Success);

        var importService = ResolveService<IBoardPackageImportService>();
        var importResult = await importService.ImportBoardPackageAsync(
            new ImportBoardPackageRequest("Round Trip Copy", exportResult.Data!.Content),
            ActorUserId);

        Assert.True(importResult.Success);
        Assert.NotNull(importResult.Data);
        var importedBoardId = importResult.Data!.Id;
        var importedActiveCardId = DbContextForAssert.Cards
            .Where(x => x.BoardId == importedBoardId)
            .Select(x => x.BoardCardId)
            .Single();
        var importedArchivedCardId = DbContextForAssert.ArchivedCards
            .Where(x => x.BoardId == importedBoardId)
            .Select(x => x.OriginalCardId)
            .Single();
        var importedNextCardId = DbContextForAssert.BoardCardIdSequences
            .Where(x => x.BoardId == importedBoardId)
            .Select(x => x.NextCardId)
            .Single();
        Assert.Equal(activeCardId, importedActiveCardId);
        Assert.Equal(archivedCardId, importedArchivedCardId);
        Assert.Equal(Math.Max(activeCardId, archivedCardId) + 1, importedNextCardId);
        Assert.Equal(deletedCardId, importedNextCardId);
    }

    [Fact]
    public async Task ImportBoardPackageAsync_WhenAnotherBoardUsesArchiveCardId_ShouldPreserveBoardScopedId()
    {
        var existingBoard = CreateBoard("Existing Archive Board")
            .AddColumn("Todo")
            .Build();

        DbContextForArrange.ArchivedCards.Add(new EntityArchivedCard
        {
            BoardId = existingBoard.BoardId,
            OriginalCardId = 777,
            ArchivedAtUtc = new DateTime(2026, 04, 20, 9, 0, 0, DateTimeKind.Utc),
            SnapshotJson = """{"schema":"archived-card","version":1,"capturedAtUtc":"2026-04-20T09:00:00Z","payload":{"title":"Existing"}}""",
            SearchTitle = "Existing",
            SearchTagsJson = "[]",
            SearchTextNormalised = "EXISTING"
        });
        await DbContextForArrange.SaveChangesAsync();

        var manifest = BoardPackageContract.CreateManifest("0.3.0");
        var payload = new BoardPackageBoardDto(
            "Archive Collision Board",
            "Archive collision board description",
            [new BoardPackageCardTypeDto("Story", null, true)],
            [],
            []);
        var archivePayload = new BoardPackageArchiveDto(
            [
                new BoardPackageArchivedCardDto(
                    777,
                    "Colliding archived card",
                    [],
                    new DateTime(2026, 04, 20, 11, 0, 0, DateTimeKind.Utc),
                    """{"schema":"archived-card","version":1,"capturedAtUtc":"2026-04-20T11:00:00Z","payload":{"title":"Colliding archived card"}}""")
            ]);

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackage(manifest, payload, archivePayload)),
            ActorUserId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var boardId = result.Data!.Id;
        var archivedCard = DbContextForAssert.ArchivedCards.Single(x => x.BoardId == boardId);
        Assert.Equal(777, archivedCard.OriginalCardId);
    }

    [Fact]
    public async Task ImportBoardPackageAsync_WithLargeArchivePayload_ShouldImportAllArchivedCards()
    {
        var manifest = BoardPackageContract.CreateManifest("0.3.0");
        var payload = new BoardPackageBoardDto(
            "Large Archive Board",
            "Large archive board description",
            [new BoardPackageCardTypeDto("Story", null, true)],
            [],
            []);
        const int archivedCardCount = 1_200;
        var archivedCards = Enumerable.Range(1, archivedCardCount)
            .Select(x => new BoardPackageArchivedCardDto(
                x,
                $"Archived {x}",
                ["Load"],
                new DateTime(2026, 04, 20, 12, 0, 0, DateTimeKind.Utc).AddMinutes(x),
                $"{{\"schema\":\"archived-card\",\"version\":1,\"capturedAtUtc\":\"2026-04-20T12:00:00Z\",\"payload\":{{\"title\":\"Archived {x}\"}}}}"))
            .ToList();
        var archivePayload = new BoardPackageArchiveDto(archivedCards);

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackage(manifest, payload, archivePayload)),
            ActorUserId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var boardId = result.Data!.Id;
        var importedArchivedCards = DbContextForAssert.ArchivedCards
            .Where(x => x.BoardId == boardId)
            .ToList();
        Assert.Equal(archivedCardCount, importedArchivedCards.Count);
        Assert.Contains(importedArchivedCards, x => x.OriginalCardId == 1 && x.SearchTitle == "Archived 1");
        Assert.Contains(importedArchivedCards, x => x.OriginalCardId == archivedCardCount && x.SearchTitle == $"Archived {archivedCardCount}");
    }

    [Fact]
    public async Task ImportBoardPackageAsync_WhenSystemCardTypeIsRenamed_ShouldImportWithRenamedSystemType()
    {
        var manifest = BoardPackageContract.CreateManifest("0.3.0");
        var payload = new BoardPackageBoardDto(
            "Renamed System Type Board",
            "Renamed system type board description",
            [
                new BoardPackageCardTypeDto("Work Item", "📘", true, "solid", """{"backgroundColor":"#FFFFFF","textColorMode":"auto"}"""),
                new BoardPackageCardTypeDto("Bug", "🐞", false, "gradient", """{"leftColor":"#F6D32D","rightColor":"#C64600","textColorMode":"auto"}""")
            ],
            [],
            [
                new BoardPackageColumnDto(
                    "Todo",
                    [
                        new BoardPackageCardDto("Fix login", "Investigate and fix", "Bug", []),
                        new BoardPackageCardDto("Audit auth flow", "Cross-check config and docs", "Work Item", [])
                    ])
            ]);

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackage(manifest, payload)),
            ActorUserId);

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("Renamed system type board description", result.Data!.Description);

        var boardId = result.Data.Id;
        var cardTypes = DbContextForAssert.CardTypes.Where(x => x.BoardId == boardId).OrderBy(x => x.Name).ToList();
        Assert.Equal(["Bug", "Work Item"], cardTypes.Select(x => x.Name).ToArray());
        Assert.Contains(
            cardTypes,
            x => x.Name == "Work Item"
                && x.IsSystem
                && x.Emoji == "📘"
                && x.StyleName == "solid"
                && x.StylePropertiesJson == """{"backgroundColor":"#FFFFFF","textColorMode":"auto","borderMode":"auto"}""");
    }

    [Fact]
    public async Task ImportBoardPackageAsync_WhenSchemaVersionIsFuture_ShouldReturnBadRequestAndWriteNothing()
    {
        var manifest = new BoardPackageManifestDto(
            BoardPackageContract.PackageFormat,
            BoardPackageContract.CurrentSchemaVersion + 1,
            "999.0.0",
            [new BoardPackageManifestEntryDto(BoardPackageContract.BoardEntryKind, BoardPackageContract.BoardEntryPath)]);
        var payload = new BoardPackageBoardDto(
            "Future Board",
            "Future board description",
            [new BoardPackageCardTypeDto("Story", null, true)],
            [],
            []);

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackage(manifest, payload)),
            ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("manifest.schemaVersion", result.ValidationErrors!.Keys);
        Assert.Empty(DbContextForAssert.Boards.Where(x => x.Name == "Future Board"));
    }

    [Fact]
    public async Task ImportBoardPackageAsync_WhenSchemaVersionIsFuture_ShouldFailBeforeParsingBoardPayload()
    {
        var manifest = new BoardPackageManifestDto(
            BoardPackageContract.PackageFormat,
            BoardPackageContract.CurrentSchemaVersion + 1,
            "999.0.0",
            [new BoardPackageManifestEntryDto(BoardPackageContract.BoardEntryKind, BoardPackageContract.BoardEntryPath)]);

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(
                "Schema Precedence",
                BuildBoardPackageWithRawBoardPayload(manifest, "{")),
            ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("manifest.schemaVersion", result.ValidationErrors!.Keys);
        Assert.DoesNotContain("board", result.ValidationErrors.Keys);
        Assert.Empty(DbContextForAssert.Boards.Where(x => x.Name == "Schema Precedence"));
    }

    [Fact]
    public async Task ImportBoardPackageAsync_WhenTagNamesCollideByCase_ShouldReturnBadRequestAndWriteNothing()
    {
        var manifest = BoardPackageContract.CreateManifest("0.3.0");
        var payload = new BoardPackageBoardDto(
            "Collision Board",
            "Collision board description",
            [new BoardPackageCardTypeDto("Story", null, true)],
            [
                new BoardPackageTagDto("Urgent", "solid", """{"backgroundColor":"#ED333B","textColorMode":"auto"}""", null),
                new BoardPackageTagDto("urgent", "solid", """{"backgroundColor":"#224466","textColorMode":"auto"}""", null)
            ],
            [new BoardPackageColumnDto("Todo", [])]);

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackage(manifest, payload)),
            ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("board.tags[1].name", result.ValidationErrors!.Keys);
        Assert.Empty(DbContextForAssert.Boards.Where(x => x.Name == "Collision Board"));
    }

    [Fact]
    public async Task ImportBoardPackageAsync_WhenCardSlickNameDoesNotExistInSlickList_ShouldReturnBadRequestAndWriteNothing()
    {
        var manifest = BoardPackageContract.CreateManifest("0.3.0");
        var payload = new BoardPackageBoardDto(
            "Missing Slick Board",
            "Missing slick board description",
            [new BoardPackageCardTypeDto("Story", null, true)],
            [],
            [
                new BoardPackageColumnDto(
                    "Todo",
                    [
                        new BoardPackageCardDto("Card A", "Description", "Story", [], SlickName: "Release train")
                    ])
            ],
            []);

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackage(manifest, payload)),
            ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("board.columns[0].cards[0].slickName", result.ValidationErrors!.Keys);
        Assert.Empty(DbContextForAssert.Boards.Where(x => x.Name == "Missing Slick Board"));
    }

    [Fact]
    public async Task ImportBoardPackageAsync_WhenPayloadIsNotZip_ShouldReturnBadRequestAndWriteNothing()
    {
        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest("Broken", [0x01, 0x02, 0x03]),
            ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("file", result.ValidationErrors!.Keys);
        Assert.Empty(DbContextForAssert.Boards.Where(x => x.Name == "Broken"));
    }

    [Fact]
    public async Task ImportBoardPackageAsync_WhenArchivePayloadIsJsonNull_ShouldReturnBadRequestAndWriteNothing()
    {
        var manifest = BoardPackageContract.CreateManifest("0.3.0");
        var payload = new BoardPackageBoardDto(
            "Archive Null Board",
            "Archive null board description",
            [new BoardPackageCardTypeDto("Story", null, true)],
            [],
            []);

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackageWithRawArchivePayload(manifest, payload, "null")),
            ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("archive", result.ValidationErrors!.Keys);
        Assert.Empty(DbContextForAssert.Boards.Where(x => x.Name == "Archive Null Board"));
    }

    [Fact]
    public async Task ImportBoardPackageAsync_WhenPresetIndexIsLegacyIntegerString_ShouldCanonicaliseToNumber()
    {
        var payload = new BoardPackageBoardDto(
            "Legacy Preset Board",
            "Legacy preset compatibility",
            [new BoardPackageCardTypeDto("Story", null, true)],
            [new BoardPackageTagDto("Legacy preset", "presets", """{"presetIndex":"4"}""", null)],
            [new BoardPackageColumnDto("Todo", [])]);
        var packageContent = BuildBoardPackage(BoardPackageContract.CreateManifest("0.3.0"), payload);

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, packageContent),
            ActorUserId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var importedTag = DbContextForAssert.Tags.Single(x => x.BoardId == result.Data!.Id);
        Assert.Equal("presets", importedTag.StyleName);
        Assert.Equal("""{"presetIndex":4}""", importedTag.StylePropertiesJson);
    }

    [Fact]
    public async Task ImportBoardPackageAsync_WhenStyleShapeIsInvalid_ShouldReturnBadRequest()
    {
        var payload = new BoardPackageBoardDto(
            "Loose Style Board",
            "Style json shape is frontend-owned",
            [new BoardPackageCardTypeDto("Story", null, true, "solid", """{"unexpected":"value"}""")],
            [new BoardPackageTagDto("Urgent", "presets", """{"any":"thing","nested":{"x":1}}""", null)],
            [new BoardPackageColumnDto("Todo", [])]);
        var packageContent = BuildBoardPackage(BoardPackageContract.CreateManifest("0.3.0"), payload);

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(new ImportBoardPackageRequest("Loose Style Board", packageContent), ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains(result.ValidationErrors!.Keys, x => x.EndsWith("stylePropertiesJson", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportBoardPackageAsync_WhenStyleJsonIsNotObject_ShouldReturnBadRequest()
    {
        var payload = new BoardPackageBoardDto(
            "Bad Style Json Board",
            "Style json must be object",
            [new BoardPackageCardTypeDto("Story", null, true, "solid", """["bad"]""")],
            [],
            [new BoardPackageColumnDto("Todo", [])]);
        var packageContent = BuildBoardPackage(BoardPackageContract.CreateManifest("0.3.0"), payload);

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(new ImportBoardPackageRequest("Bad Style Json Board", packageContent), ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains(result.ValidationErrors!.Keys, key => key.Contains("stylePropertiesJson", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ImportBoardPackageAsync_WithStaticCustomFields_ShouldPreserveDefinitionsAndValues(int schemaVersion)
    {
        var imported = await ResolveService<IBoardPackageImportService>().ImportBoardPackageAsync(
            new ImportBoardPackageRequest("Imported", CustomFieldPackage(schemaVersion, CustomFieldsBoardJson)), ActorUserId);

        Assert.True(imported.Success, imported.Message);
        var importedTypes = await DbContextForAssert.CardTypes.Where(type => type.BoardId == imported.Data!.Id).ToListAsync();
        var systemFields = CardFieldJsonCodec.DeserialiseDefinitions(importedTypes.Single(type => type.IsSystem).FieldDefinitionsJson);
        var customFields = CardFieldJsonCodec.DeserialiseDefinitions(importedTypes.Single(type => !type.IsSystem).FieldDefinitionsJson);
        Assert.Equal(new[] { "Ref", "Points", "Due" }, systemFields.Select(field => field.FieldCode));
        Assert.Equal(new[] { "Due", "Points", "Ref" }, customFields.Select(field => field.FieldCode));
        var reference = Assert.IsType<TextFieldDefinition>(systemFields[0]);
        Assert.Equal("Reference", reference.DisplayName);
        Assert.Equal("🔖", reference.Emoji);
        Assert.True(reference.ShowOnCard);
        Assert.Equal(25, reference.MaxLength);
        var estimate = Assert.IsType<IntegerFieldDefinition>(systemFields[1]);
        Assert.Equal(0, estimate.DefaultValue);
        Assert.Equal(0, estimate.Min);
        Assert.Equal(10, estimate.Max);
        Assert.IsType<DateFieldDefinition>(systemFields[2]);
        Assert.Equal(systemFields.Reverse(), customFields);
        Assert.Equal(2, imported.Data!.Columns.SelectMany(column => column.Cards).Count());
        foreach (var card in imported.Data.Columns.SelectMany(column => column.Cards))
        {
            Assert.Equal(JsonValueKind.Null, card.Fields["Ref"].ValueKind);
            Assert.Equal(0, card.Fields["Points"].GetInt32());
            Assert.Equal("2024-02-29", card.Fields["Due"].GetString());
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Import_LegacyPayloadWithoutFields_ShouldRemainValid(int schemaVersion)
    {
        const string boardJson = """
            {"name":"Legacy","cardTypes":[{"name":"Story","isSystem":true}],"tags":[],
            "columns":[{"title":"Todo","cards":[{"id":1,"title":"Legacy card","description":"","cardTypeName":"Story","tagNames":[]}]}]}
            """;

        var result = await ResolveService<IBoardPackageImportService>().ImportBoardPackageAsync(
            new ImportBoardPackageRequest("Legacy", CustomFieldPackage(schemaVersion, boardJson)), ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Empty(Assert.Single(Assert.Single(result.Data!.Columns).Cards).Fields);
        Assert.Equal("[]", (await DbContextForAssert.CardTypes.SingleAsync()).FieldDefinitionsJson);
    }

    [Theory]
    [InlineData("invalid-default")]
    [InlineData("unknown-value")]
    [InlineData("missing-value")]
    [InlineData("range")]
    [InlineData("null-definitions")]
    [InlineData("null-values")]
    [InlineData("variant")]
    [InlineData("missing-discriminator")]
    [InlineData("duplicate-code")]
    public async Task Import_InvalidFieldData_ShouldRejectWithoutCreatingBoard(string error)
    {
        var json = JsonNode.Parse(CustomFieldsBoardJson)!;
        var field = json["cardTypes"]![0]!["fields"]![1]!;
        var values = json["columns"]![0]!["cards"]![0]!["fields"]!.AsObject();
        switch (error)
        {
            case "invalid-default": field["defaultValue"] = 11; break;
            case "unknown-value": values["Unknown"] = 1; break;
            case "missing-value": values.Remove("Points"); break;
            case "range": values["Points"] = 11; break;
            case "null-definitions": json["cardTypes"]![0]!["fields"] = null; break;
            case "null-values": json["columns"]![0]!["cards"]![0]!["fields"] = null; break;
            case "variant": field["maxLength"] = 10; break;
            case "missing-discriminator": field.AsObject().Remove("type"); break;
            case "duplicate-code": values["points"] = 7; break;
        }

        var result = await ResolveService<IBoardPackageImportService>().ImportBoardPackageAsync(
            new ImportBoardPackageRequest("Invalid", CustomFieldPackage(4, json.ToJsonString())), ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotEmpty(result.ValidationErrors!);
        Assert.Empty(await DbContextForAssert.Boards.ToListAsync());
    }

    [Fact]
    public async Task Import_DefinitionDiscriminatorMayAppearLast()
    {
        var json = JsonNode.Parse(CustomFieldsBoardJson)!;
        var definition = json["cardTypes"]![0]!["fields"]![0]!.AsObject();
        var kind = definition["type"]!.GetValue<string>();
        definition.Remove("type");
        definition["type"] = kind;

        var result = await ResolveService<IBoardPackageImportService>().ImportBoardPackageAsync(
            new ImportBoardPackageRequest("Out of order", CustomFieldPackage(4, json.ToJsonString())), ActorUserId);

        Assert.True(result.Success, result.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExportImportArchive_ShouldPreserveTypeIdentityAcrossNewIds(bool replaceType)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Archived").Build();
        var card = board.GetCard("Archived");
        var original = new EntityCardType
        {
            BoardId = board.BoardId, Name = "Custom", StyleName = "auto", StylePropertiesJson = "{}",
            FieldDefinitionsJson = CardFieldJsonCodec.SerialiseDefinitions(
                [new IntegerFieldDefinition { FieldCode = "Points", DisplayName = "Estimate", DefaultValue = 0 }])
        };
        DbContextForArrange.CardTypes.Add(original);
        card.CardType = original;
        card.FieldValuesJson = """{"Points":7}""";
        await DbContextForArrange.SaveChangesAsync();
        var archiveService = ResolveService<ICardArchiveService>();
        Assert.True((await archiveService.ArchiveCardAsync(board.BoardId, card.BoardCardId, ActorUserId)).Success);
        if (replaceType)
        {
            DbContextForArrange.Entry(card).State = EntityState.Detached;
            DbContextForArrange.CardTypes.Remove(original);
            await DbContextForArrange.SaveChangesAsync();
            DbContextForArrange.CardTypes.Add(new EntityCardType
            {
                BoardId = board.BoardId, Name = "Custom", StyleName = "auto", StylePropertiesJson = "{}",
                FieldDefinitionsJson = CardFieldJsonCodec.SerialiseDefinitions(
                    [new IntegerFieldDefinition { FieldCode = "Effort", DisplayName = "Estimate", DefaultValue = 0 }])
            });
        }
        else
        {
            original.FieldDefinitionsJson = CardFieldJsonCodec.SerialiseDefinitions(
                [new IntegerFieldDefinition { FieldCode = "Points", DisplayName = "Renamed", DefaultValue = 0 }]);
        }
        await DbContextForArrange.SaveChangesAsync();
        var export = await ResolveService<IBoardExportService>().ExportBoardAsync(board.BoardId, ActorUserId, "test");
        Assert.True(export.Success, export.Message);
        using var content = export.Data!.Content;
        var imported = await ResolveService<IBoardPackageImportService>().ImportBoardPackageAsync(
            new ImportBoardPackageRequest("Imported archive", content), ActorUserId);
        Assert.True(imported.Success, imported.Message);

        var restored = await archiveService.UnarchiveCardAsync(imported.Data!.Id, card.BoardCardId, ActorUserId);

        Assert.True(restored.Success, restored.Message);
        Assert.NotEqual(original.Id, restored.Data!.CardTypeId);
        Assert.Equal(7, restored.Data.Fields[replaceType ? "Effort" : "Points"].GetInt32());
        Assert.DoesNotContain("Fields removed", restored.Data.Description);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ImportArchive_WithoutSourceTypeMapping_ShouldNotMistakeCollidingIdForSameType(int version)
    {
        const string boardJson = """
            {"name":"Collision","cardTypes":[{"name":"Story","isSystem":true,"fields":[
              {"type":"integer","fieldCode":"Effort","displayName":"Estimate","defaultValue":0}
            ]}],"tags":[],"columns":[{"title":"Todo","cards":[]}]}
            """;
        var archived = new BoardPackageArchiveDto(
            [new BoardPackageArchivedCardDto(99, "Archived", [], DateTime.UtcNow, CustomFieldArchiveSnapshot)]);
        var imported = await ResolveService<IBoardPackageImportService>().ImportBoardPackageAsync(
            new ImportBoardPackageRequest("Collision", CustomFieldPackage(version, boardJson, archived)), ActorUserId);
        Assert.True(imported.Success, imported.Message);
        Assert.Equal(1, (await DbContextForAssert.CardTypes.SingleAsync()).Id);

        var restored = await ResolveService<ICardArchiveService>().UnarchiveCardAsync(imported.Data!.Id, 99, ActorUserId);

        Assert.True(restored.Success, restored.Message);
        Assert.Equal(7, restored.Data!.Fields["Effort"].GetInt32());
        Assert.DoesNotContain("Fields removed", restored.Data.Description);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("range")]
    [InlineData("definition")]
    [InlineData("shape")]
    public async Task Import_InvalidArchivedFields_ShouldRejectBeforeWriting(string error)
    {
        var snapshot = JsonNode.Parse(CustomFieldArchiveSnapshot)!;
        var payload = snapshot["payload"]!;
        switch (error)
        {
            case "unknown": payload["fields"]!["Unknown"] = 3; break;
            case "range": payload["fields"]!["Points"] = 11; break;
            case "definition": payload["fieldDefinitions"]![0]!["min"] = 5; break;
            case "shape": payload["fields"] = new JsonArray(); break;
        }
        var archived = new BoardPackageArchiveDto(
            [new BoardPackageArchivedCardDto(99, "Archived", [], DateTime.UtcNow, snapshot.ToJsonString())]);

        var result = await ResolveService<IBoardPackageImportService>().ImportBoardPackageAsync(
            new ImportBoardPackageRequest("Invalid", CustomFieldPackage(4, CustomFieldsBoardJson, archived)), ActorUserId);

        Assert.False(result.Success);
        Assert.Contains(result.ValidationErrors!.Keys, key => key.StartsWith("archive.cards[0].snapshotJson"));
        Assert.Empty(await DbContextForAssert.Boards.ToListAsync());
    }

    [Fact]
    public async Task Import_UnknownArchiveVersion_ShouldPreserveOpaqueSnapshot()
    {
        const string snapshot = """{"schema":"archived-card","version":999,"payload":{"fields":["future"]}}""";
        var archived = new BoardPackageArchiveDto(
            [new BoardPackageArchivedCardDto(99, "Future", [], DateTime.UtcNow, snapshot)]);

        var result = await ResolveService<IBoardPackageImportService>().ImportBoardPackageAsync(
            new ImportBoardPackageRequest("Future", CustomFieldPackage(4, CustomFieldsBoardJson, archived)), ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(snapshot, (await DbContextForAssert.ArchivedCards.SingleAsync()).SnapshotJson);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ImportArchive_ShouldPersistNormalisedDefinitionsAndValuesForDetailReads(int version)
    {
        var imported = await ResolveService<IBoardPackageImportService>().ImportBoardPackageAsync(
            new ImportBoardPackageRequest("Normalise archive", CustomFieldPackage(version, CustomFieldsBoardJson, rawArchiveJson: NormalisationArchiveJson)), ActorUserId);
        Assert.True(imported.Success, imported.Message);

        var detail = await ResolveService<ICardArchiveService>().GetArchivedCardAsync(imported.Data!.Id, 99, ActorUserId);

        Assert.True(detail.Success, detail.Message);
        Assert.Equal(new[] { "Points", "Ref", "Empty" }, detail.Data!.FieldDefinitions.Select(field => field.FieldCode));
        Assert.Equal("Estimate", detail.Data.FieldDefinitions[0].DisplayName);
        Assert.Equal("Default", Assert.IsType<TextFieldDefinition>(detail.Data.FieldDefinitions[1]).DefaultValue);
        Assert.Equal(7, detail.Data.Card.Fields["Points"].GetInt32());
        Assert.Equal("Saved", detail.Data.Card.Fields["Ref"].GetString());
        Assert.Equal(JsonValueKind.Null, detail.Data.Card.Fields["Empty"].ValueKind);
        Assert.All(detail.Data.FieldDefinitions, field => Assert.True(detail.Data.Card.Fields.ContainsKey(field.FieldCode)));
        var stored = await DbContextForAssert.ArchivedCards.SingleAsync();
        using var saved = JsonDocument.Parse(stored.SnapshotJson);
        Assert.Equal("""{"Points":7,"Ref":"Saved","Empty":null}""", saved.RootElement.GetProperty("payload").GetProperty("fields").GetRawText());
    }

    private const string CustomFieldArchiveSnapshot = """
        {"schema":"archived-card","version":1,"capturedAtUtc":"2026-10-04T12:00:00Z","payload":{
        "boardId":1,"originalCardId":99,"boardColumnId":1,"originalColumnName":"Todo",
        "cardTypeId":1,"cardTypeName":"Story","title":"Archived","description":"","sortKey":"A",
        "tags":[],"tagNames":[],"createdAtUtc":"2026-10-04T12:00:00Z","updatedAtUtc":"2026-10-04T12:00:00Z",
        "fieldDefinitions":[{"type":"integer","fieldCode":"Points","displayName":"Estimate","defaultValue":0,"max":10}],
        "fields":{"Points":7}}}
        """;

    private const string CustomFieldsBoardJson = """
        {
          "name": "Fields",
          "description": "",
          "cardTypes": [
            {
              "id": 51,
              "name": "Story",
              "isSystem": true,
              "fields": [
                {
                  "type": "text",
                  "fieldCode": "Ref",
                  "displayName": "Reference",
                  "nullable": true,
                  "emoji": "🔖",
                  "showOnCard": true,
                  "maxLength": 25
                },
                {
                  "type": "integer",
                  "fieldCode": "Points",
                  "displayName": "Estimate",
                  "defaultValue": 0,
                  "min": 0,
                  "max": 10
                },
                {
                  "type": "date",
                  "fieldCode": "Due",
                  "displayName": "Due date",
                  "nullable": true
                }
              ]
            },
            {
              "id": 52,
              "name": "Custom",
              "isSystem": false,
              "fields": [
                {
                  "type": "date",
                  "fieldCode": "Due",
                  "displayName": "Due date",
                  "nullable": true
                },
                {
                  "type": "integer",
                  "fieldCode": "Points",
                  "displayName": "Estimate",
                  "defaultValue": 0,
                  "min": 0,
                  "max": 10
                },
                {
                  "type": "text",
                  "fieldCode": "Ref",
                  "displayName": "Reference",
                  "nullable": true,
                  "emoji": "🔖",
                  "showOnCard": true,
                  "maxLength": 25
                }
              ]
            }
          ],
          "tags": [],
          "columns": [
            {
              "title": "Todo",
              "cards": [
                {
                  "id": 1,
                  "title": "System",
                  "description": "",
                  "cardTypeName": "Story",
                  "tagNames": [],
                  "fields": {
                    "Ref": null,
                    "Points": 0,
                    "Due": "2024-02-29"
                  }
                },
                {
                  "id": 2,
                  "title": "Custom",
                  "description": "",
                  "cardTypeName": "Custom",
                  "tagNames": [],
                  "fields": {
                    "Ref": null,
                    "Points": 0,
                    "Due": "2024-02-29"
                  }
                }
              ]
            }
          ]
        }
        """;

    private const string NormalisationArchiveJson = """
        {
          "cards": [
            {
              "originalCardId": 99,
              "title": "Archived",
              "tagNames": [],
              "archivedAtUtc": "2026-10-04T12:00:00Z",
              "snapshotJson": "{\"schema\":\"archived-card\",\"version\":1,\"capturedAtUtc\":\"2026-10-04T12:00:00Z\",\"payload\":{\"boardId\":1,\"originalCardId\":99,\"boardColumnId\":1,\"originalColumnName\":\"Todo\",\"cardTypeId\":51,\"cardTypeName\":\"Story\",\"title\":\"Archived\",\"description\":\"\",\"sortKey\":\"A\",\"tags\":[],\"tagNames\":[],\"createdAtUtc\":\"2026-10-04T12:00:00Z\",\"updatedAtUtc\":\"2026-10-04T12:00:00Z\",\"fieldDefinitions\":[{\"type\":\"integer\",\"fieldCode\":\" Points \",\"displayName\":\" Estimate \",\"defaultValue\":0},{\"type\":\"text\",\"fieldCode\":\"Ref\",\"displayName\":\"Reference\",\"nullable\":true,\"defaultValue\":\" Default \"},{\"type\":\"text\",\"fieldCode\":\"Empty\",\"displayName\":\"Empty\",\"nullable\":true}],\"fields\":{\"points\":7,\"ref\":\" Saved \",\"empty\":\"   \"}}}"
            }
          ]
        }
        """;

    private static byte[] CustomFieldPackage(int version, string boardJson,
        BoardPackageArchiveDto? archived = null, string? rawArchiveJson = null) =>
        BuildBoardPackageWithRawBoardPayload(
            BoardPackageContract.CreateManifest("test") with { SchemaVersion = version },
            boardJson, archived, rawArchiveJson);

    private static byte[] BuildBoardPackage(
        BoardPackageManifestDto manifest,
        BoardPackageBoardDto boardPayload,
        BoardPackageArchiveDto? archivePayload = null)
    {
        boardPayload = AddSchemaThreeIdentityWhenMissing(manifest, boardPayload, archivePayload);
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteJsonEntry(archive, BoardPackageContract.ManifestPath, manifest);
            WriteJsonEntry(archive, BoardPackageContract.BoardEntryPath, boardPayload);
            if (manifest.Entries.Any(x => x.Kind == BoardPackageContract.ArchiveEntryKind && x.Path == BoardPackageContract.ArchiveEntryPath))
            {
                WriteJsonEntry(archive, BoardPackageContract.ArchiveEntryPath, archivePayload ?? new BoardPackageArchiveDto([]));
            }
        }

        return stream.ToArray();
    }

    private static BoardPackageBoardDto AddSchemaThreeIdentityWhenMissing(
        BoardPackageManifestDto manifest,
        BoardPackageBoardDto boardPayload,
        BoardPackageArchiveDto? archivePayload)
    {
        if (manifest.SchemaVersion < 3)
        {
            return boardPayload;
        }

        var assignedIds = (archivePayload?.Cards ?? [])
            .Select(x => x.OriginalCardId)
            .Where(x => x > 0)
            .ToHashSet();
        var nextAvailableId = 1;
        var columns = boardPayload.Columns
            .Select(column => column with
            {
                Cards = column.Cards
                    .Select(card =>
                    {
                        if (card.Id.HasValue)
                        {
                            assignedIds.Add(card.Id.Value);
                            return card;
                        }

                        while (assignedIds.Contains(nextAvailableId))
                        {
                            nextAvailableId++;
                        }

                        var cardWithId = card with { Id = nextAvailableId };
                        assignedIds.Add(nextAvailableId);
                        nextAvailableId++;
                        return cardWithId;
                    })
                    .ToList()
            })
            .ToList();
        return boardPayload with
        {
            Columns = columns
        };
    }

    private static void WriteJsonEntry<T>(ZipArchive archive, string path, T payload)
    {
        if (payload is BoardPackageManifestDto { SchemaVersion: >= 4 })
        {
            WriteJsonEntry(archive, BoardPackageContract.AttachmentsEntryPath, new BoardPackageAttachmentsDto([]));
        }
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static byte[] BuildBoardPackageWithRawBoardPayload(
        BoardPackageManifestDto manifest,
        string rawBoardPayload,
        BoardPackageArchiveDto? archivePayload = null,
        string? rawArchivePayload = null)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteJsonEntry(archive, BoardPackageContract.ManifestPath, manifest);
            var boardEntry = archive.CreateEntry(BoardPackageContract.BoardEntryPath, CompressionLevel.Optimal);
            using (var writer = new StreamWriter(boardEntry.Open()))
            {
                writer.Write(rawBoardPayload);
            }
            if (manifest.Entries.Any(x => x.Kind == BoardPackageContract.ArchiveEntryKind && x.Path == BoardPackageContract.ArchiveEntryPath))
            {
                if (rawArchivePayload is null)
                {
                    WriteJsonEntry(archive, BoardPackageContract.ArchiveEntryPath, archivePayload ?? new BoardPackageArchiveDto([]));
                }
                else
                {
                    using var writer = new StreamWriter(archive.CreateEntry(BoardPackageContract.ArchiveEntryPath).Open());
                    writer.Write(rawArchivePayload);
                }
            }
        }

        return stream.ToArray();
    }

    private static byte[] BuildBoardPackageWithRawArchivePayload(
        BoardPackageManifestDto manifest,
        BoardPackageBoardDto boardPayload,
        string rawArchivePayload)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteJsonEntry(archive, BoardPackageContract.ManifestPath, manifest);
            WriteJsonEntry(archive, BoardPackageContract.BoardEntryPath, boardPayload);
            var archiveEntry = archive.CreateEntry(BoardPackageContract.ArchiveEntryPath, CompressionLevel.Optimal);
            using var writer = new StreamWriter(archiveEntry.Open());
            writer.Write(rawArchivePayload);
        }

        return stream.ToArray();
    }
}
