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
    public async Task ImportBoardPackageAsync_WhenSystemCardTypeIsRenamed_ShouldImportWithRenamedSystemType()
    {
        var manifest = BoardPackageContract.CreateManifest("0.3.0");
        var payload = new BoardPackageBoardDto(
            "Renamed System Type Board",
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

        var boardId = result.Data!.Id;
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
    public async Task ImportBoardPackageAsync_WhenSchemaVersionIsFuture_ShouldReturnBadRequestAndWriteNothing()
    {
        var manifest = new BoardPackageManifestDto(
            BoardPackageContract.PackageFormat,
            BoardPackageContract.CurrentSchemaVersion + 1,
            "999.0.0",
            [new BoardPackageManifestEntryDto(BoardPackageContract.BoardEntryKind, BoardPackageContract.BoardEntryPath)]);
        var payload = new BoardPackageBoardDto(
            "Future Board",
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

    private static byte[] BuildBoardPackage(BoardPackageManifestDto manifest, BoardPackageBoardDto boardPayload)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteJsonEntry(archive, BoardPackageContract.ManifestPath, manifest);
            WriteJsonEntry(archive, BoardPackageContract.BoardEntryPath, boardPayload);
        }

        return stream.ToArray();
    }

    private static void WriteJsonEntry<T>(ZipArchive archive, string path, T payload)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static byte[] BuildBoardPackageWithRawBoardPayload(BoardPackageManifestDto manifest, string rawBoardPayload)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteJsonEntry(archive, BoardPackageContract.ManifestPath, manifest);
            var boardEntry = archive.CreateEntry(BoardPackageContract.BoardEntryPath, CompressionLevel.Optimal);
            using var writer = new StreamWriter(boardEntry.Open());
            writer.Write(rawBoardPayload);
        }

        return stream.ToArray();
    }
}
