using System.IO.Compression;
using System.Text.Json;
using BoardOil.Abstractions.Board;
using BoardOil.Contracts.Board;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Services.Board;
using BoardOil.Services.Tag;
using BoardOil.Services.Tests.Infrastructure;
using BoardOil.TasksMd;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class BoardImportServiceTests : TestBaseDb
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly FakeTasksMdClient _tasksMdClient = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<ITasksMdClient>();
        services.AddSingleton<ITasksMdClient>(_tasksMdClient);
    }

    [Fact]
    public async Task ImportTasksMdBoardAsync_ShouldCreateBoardWithImportedColumnsCardsTagsAndOwner()
    {
        _tasksMdClient.Model = new TasksMdBoardImportModel(
            [
                new TasksMdImportedColumn(
                    "Todo",
                    [
                        new TasksMdImportedCard("Card A", "Description", ["Urgent", "MissingTag"]),
                        new TasksMdImportedCard("Card B", string.Empty, [])
                    ]),
                new TasksMdImportedColumn("Done", [new TasksMdImportedCard("Card C", "Done now", ["Urgent"])])
            ],
            [new TasksMdImportedTag("Urgent", "#bf616a")]);

        var service = ResolveService<IBoardTasksMdImportService>();
        var result = await service.ImportTasksMdBoardAsync(
            new ImportTasksMdBoardRequest("https://tasks.example.net/"),
            ActorUserId);

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("tasks.example.net", result.Data!.Name);
        Assert.Equal(["Todo", "Done"], result.Data.Columns.Select(x => x.Title).ToArray());
        Assert.Equal(["Card A", "Card B"], result.Data.Columns[0].Cards.Select(x => x.Title).ToArray());

        var boardId = result.Data.Id;
        var board = DbContextForAssert.Boards.Single(x => x.Id == boardId);
        Assert.Equal("tasks.example.net", board.Name);

        var ownerMembership = DbContextForAssert.BoardMembers.Single(x => x.BoardId == boardId && x.UserId == ActorUserId);
        Assert.Equal(BoardMemberRole.Owner, ownerMembership.Role);

        var columns = DbContextForAssert.Columns.Where(x => x.BoardId == boardId).OrderBy(x => x.SortKey).ToList();
        Assert.Equal(["Todo", "Done"], columns.Select(x => x.Title).ToArray());
        var systemCardType = DbContextForAssert.CardTypes.Single(x => x.BoardId == boardId && x.IsSystem);
        Assert.Equal("Story", systemCardType.Name);
        Assert.Null(systemCardType.Emoji);

        var todoCards = DbContextForAssert.Cards
            .Where(x => x.BoardColumnId == columns[0].Id)
            .OrderBy(x => x.SortKey)
            .ToList();
        Assert.Equal(["Card A", "Card B"], todoCards.Select(x => x.Title).ToArray());
        Assert.All(todoCards, x => Assert.Equal(systemCardType.Id, x.CardTypeId));

        var tags = DbContextForAssert.Tags.Where(x => x.BoardId == boardId).OrderBy(x => x.Name).ToList();
        Assert.Equal(["MissingTag", "Urgent"], tags.Select(x => x.Name).ToArray());

        var urgentTag = tags.Single(x => x.Name == "Urgent");
        var urgentStyle = JsonDocument.Parse(urgentTag.StylePropertiesJson).RootElement;
        Assert.Equal("#bf616a", urgentStyle.GetProperty("backgroundColor").GetString());
        Assert.Equal("auto", urgentStyle.GetProperty("textColorMode").GetString());

        var missingTag = tags.Single(x => x.Name == "MissingTag");
        Assert.Equal(TagStyleSchemaValidator.SolidStyleName, missingTag.StyleName);
    }

    [Fact]
    public async Task ImportTasksMdBoardAsync_WhenUrlIsInvalid_ShouldReturnBadRequest()
    {
        var service = ResolveService<IBoardTasksMdImportService>();

        var result = await service.ImportTasksMdBoardAsync(new ImportTasksMdBoardRequest("notaurl"), ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("url", result.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task ImportTasksMdBoardAsync_WhenClientFails_ShouldReturnBadRequestAndWriteNothing()
    {
        _tasksMdClient.ExceptionToThrow = new TasksMdClientException(
            "Unable to fetch tasksmd data.",
            [new TasksMdClientValidationError("url", "Unable to fetch tasksmd data.")]);

        var service = ResolveService<IBoardTasksMdImportService>();

        var result = await service.ImportTasksMdBoardAsync(
            new ImportTasksMdBoardRequest("https://tasks.example.net/"),
            ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("url", result.ValidationErrors!.Keys);
        Assert.Empty(DbContextForAssert.Boards.Where(x => x.Name == "tasks.example.net"));
    }

    [Fact]
    public async Task ImportBoardPackageAsync_ShouldCreateBoardWithImportedColumnsCardsTagsAndCardTypes()
    {
        var manifest = BoardPackageContract.CreateManifest("0.3.0");
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
                        new BoardPackageCardDto("Fix login", "Investigate and fix", "Bug", ["Urgent", "NeedsReview"])
                    ]),
                new BoardPackageColumnDto(
                    "Done",
                    [
                        new BoardPackageCardDto("Ship release", "Already done", "Story", [])
                    ])
            ]);

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackage(manifest, payload)),
            ActorUserId);

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("Imported Package Board", result.Data!.Name);
        Assert.Equal("Imported package description", result.Data.Description);
        Assert.Equal(["Todo", "Done"], result.Data.Columns.Select(x => x.Title).ToArray());
        Assert.Equal("Bug", result.Data.Columns[0].Cards[0].CardTypeName);
        Assert.Equal(["NeedsReview", "Urgent"], result.Data.Columns[0].Cards[0].TagNames);

        var boardId = result.Data.Id;
        var ownerMembership = DbContextForAssert.BoardMembers.Single(x => x.BoardId == boardId && x.UserId == ActorUserId);
        Assert.Equal(BoardMemberRole.Owner, ownerMembership.Role);

        var cardTypes = DbContextForAssert.CardTypes.Where(x => x.BoardId == boardId).OrderBy(x => x.Name).ToList();
        Assert.Equal(["Bug", "Story"], cardTypes.Select(x => x.Name).ToArray());
        Assert.Contains(
            cardTypes,
            x => x.Name == "Story"
                && x.IsSystem
                && x.StyleName == "solid"
                && x.StylePropertiesJson == """{"backgroundColor":"#FFFFFF","textColorMode":"auto"}""");
        Assert.Contains(
            cardTypes,
            x => x.Name == "Bug"
                && !x.IsSystem
                && x.Emoji == "🐞"
                && x.StyleName == "gradient"
                && x.StylePropertiesJson == """{"leftColor":"#F6D32D","rightColor":"#C64600","textColorMode":"auto"}""");

        var tags = DbContextForAssert.Tags.Where(x => x.BoardId == boardId).OrderBy(x => x.Name).ToList();
        Assert.Equal(["NeedsReview", "Urgent"], tags.Select(x => x.Name).ToArray());
        Assert.Contains(tags, x => x.Name == "Urgent" && x.StyleName == "solid" && x.Emoji == "🟥");
        Assert.Contains(tags, x => x.Name == "NeedsReview" && x.StyleName == TagStyleSchemaValidator.SolidStyleName);
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
            CreatedAtUtc = now,
            UpdatedAtUtc = now
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
            CreatedAtUtc = now,
            UpdatedAtUtc = now
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
    public async Task ImportBoardPackageAsync_WhenArchiveOriginalCardIdCollides_ShouldAssignFallbackId()
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
        Assert.NotEqual(777, archivedCard.OriginalCardId);
        Assert.True(archivedCard.OriginalCardId < 0);
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
                && x.StylePropertiesJson == """{"backgroundColor":"#FFFFFF","textColorMode":"auto"}""");
    }

    [Fact]
    public async Task ImportBoardPackageAsync_WhenSchemaVersionIsOne_ShouldImportWithEmptyDescription()
    {
        var manifest = new BoardPackageManifestDto(
            BoardPackageContract.PackageFormat,
            1,
            "0.2.0",
            [new BoardPackageManifestEntryDto(BoardPackageContract.BoardEntryKind, BoardPackageContract.BoardEntryPath)]);
        var boardPayloadV1Json =
            """
            {
              "name": "Legacy Board",
              "cardTypes": [
                { "name": "Story", "emoji": null, "isSystem": true }
              ],
              "tags": [],
              "columns": []
            }
            """;

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackageWithRawBoardPayload(manifest, boardPayloadV1Json)),
            ActorUserId);

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("Legacy Board", result.Data!.Name);
        Assert.Equal(string.Empty, result.Data.Description);
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

    private sealed class FakeTasksMdClient : ITasksMdClient
    {
        public TasksMdBoardImportModel Model { get; set; } = new([], []);
        public Exception? ExceptionToThrow { get; set; }

        public Task<TasksMdBoardImportModel> LoadBoardAsync(Uri baseUri, CancellationToken cancellationToken = default)
        {
            _ = baseUri;
            _ = cancellationToken;
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(Model);
        }
    }

    private static byte[] BuildBoardPackage(
        BoardPackageManifestDto manifest,
        BoardPackageBoardDto boardPayload,
        BoardPackageArchiveDto? archivePayload = null)
    {
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

    private static void WriteJsonEntry<T>(ZipArchive archive, string path, T payload)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static byte[] BuildBoardPackageWithRawBoardPayload(
        BoardPackageManifestDto manifest,
        string rawBoardPayload,
        BoardPackageArchiveDto? archivePayload = null)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteJsonEntry(archive, BoardPackageContract.ManifestPath, manifest);
            var boardEntry = archive.CreateEntry(BoardPackageContract.BoardEntryPath, CompressionLevel.Optimal);
            using var writer = new StreamWriter(boardEntry.Open());
            writer.Write(rawBoardPayload);
            if (manifest.Entries.Any(x => x.Kind == BoardPackageContract.ArchiveEntryKind && x.Path == BoardPackageContract.ArchiveEntryPath))
            {
                WriteJsonEntry(archive, BoardPackageContract.ArchiveEntryPath, archivePayload ?? new BoardPackageArchiveDto([]));
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
