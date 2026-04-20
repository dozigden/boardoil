using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Tag;
using BoardOil.Services.Board;
using BoardOil.TasksMd;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class BoardImportApiIntegrationTests : TestBaseIntegration
{
    private const int MaxCardDescriptionLength = 20_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly FakeTasksMdClient _tasksMdClient = new();

    protected override BoardOilApiFactory CreateFactory(string databasePath) =>
        new(
            databasePath,
            configureTestServices: services =>
            {
                services.RemoveAll<ITasksMdClient>();
                services.AddSingleton<ITasksMdClient>(_tasksMdClient);
            });

    [Fact]
    public async Task ImportTasksMdBoard_ShouldCreateBoardFromImportedModel()
    {
        _tasksMdClient.Model = new TasksMdBoardImportModel(
            [
                new TasksMdImportedColumn(
                    "Todo",
                    [
                        new TasksMdImportedCard("Card A", "Clean description", ["Urgent", "Discovered"]),
                        new TasksMdImportedCard("Card B", string.Empty, [])
                    ]),
                new TasksMdImportedColumn("In Progress", [new TasksMdImportedCard("Card C", "Working", ["Urgent"])])
            ],
            [new TasksMdImportedTag("Urgent", "#bf616a")]);

        var response = await Client.PostAsJsonAsync(
            "/api/boards/import/tasksmd",
            new ImportTasksMdBoardRequest("https://tasks.example.net/"));
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.True(payload!.Success);
        Assert.NotNull(payload.Data);
        Assert.Equal("tasks.example.net", payload.Data!.Name);
        Assert.Equal(["Todo", "In Progress"], payload.Data.Columns.Select(x => x.Title).ToArray());
        Assert.Equal(["Card A", "Card B"], payload.Data.Columns[0].Cards.Select(x => x.Title).ToArray());
        Assert.Equal(["Discovered", "Urgent"], payload.Data.Columns[0].Cards[0].Tags.Select(x => x.Name).ToArray());
        Assert.Equal(["Discovered", "Urgent"], payload.Data.Columns[0].Cards[0].TagNames);
        Assert.Equal("Clean description", payload.Data.Columns[0].Cards[0].Description);
        Assert.Contains(payload.Data.Columns[0].Cards[0].Tags, x => x.Name == "Urgent" && x.StylePropertiesJson.Contains("#bf616a", StringComparison.OrdinalIgnoreCase));

        var tagsResponse = await Client.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<TagDto>>>(
            $"/api/boards/{payload.Data.Id}/tags",
            JsonOptions);
        Assert.NotNull(tagsResponse);
        Assert.NotNull(tagsResponse!.Data);
        var tags = tagsResponse.Data!;
        Assert.Contains(tags, x => x.Name == "Urgent" && x.StylePropertiesJson.Contains("#bf616a", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tags, x => x.Name == "Discovered");
    }

    [Fact]
    public async Task ImportTasksMdBoard_WhenUrlIsInvalid_ShouldReturnBadRequest()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/boards/import/tasksmd",
            new ImportTasksMdBoardRequest("notaurl"));

        Assert.Equal(400, (int)response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.NotNull(payload.ValidationErrors);
        Assert.Contains("url", payload.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task ImportTasksMdBoard_WhenCardDescriptionExceedsMaxLength_ShouldReturnBadRequest()
    {
        _tasksMdClient.Model = new TasksMdBoardImportModel(
            [new TasksMdImportedColumn("Todo", [new TasksMdImportedCard("Card A", new string('D', MaxCardDescriptionLength + 1), [])])],
            []);

        var response = await Client.PostAsJsonAsync(
            "/api/boards/import/tasksmd",
            new ImportTasksMdBoardRequest("https://tasks.example.net/"));

        Assert.Equal(400, (int)response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.NotNull(payload.ValidationErrors);
        Assert.Contains("columns[0].cards[0].description", payload.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task ImportBoardPackage_ShouldCreateBoardFromZipUpload()
    {
        var manifest = BoardPackageContract.CreateManifest("0.3.0");
        var payload = new BoardPackageBoardDto(
            "Imported API Board",
            "Imported API Board description",
            [
                new BoardPackageCardTypeDto("Story", null, true, "solid", """{"backgroundColor":"#FFFFFF","textColorMode":"auto"}"""),
                new BoardPackageCardTypeDto("Bug", "🐞", false, "gradient", """{"leftColor":"#F6D32D","rightColor":"#C64600","textColorMode":"auto"}""")
            ],
            [
                new BoardPackageTagDto("Urgent", "solid", """{"backgroundColor":"#ED333B","textColorMode":"auto"}""", "🧪")
            ],
            [
                new BoardPackageColumnDto(
                    "Todo",
                    [
                        new BoardPackageCardDto("Card A", "Description A", "Bug", ["Urgent", "NeedsReview"])
                    ])
            ]);

        using var requestContent = new MultipartFormDataContent();
        requestContent.Add(new StringContent("Renamed Board"), "name");
        requestContent.Add(
            new ByteArrayContent(BuildBoardPackage(manifest, payload))
            {
                Headers =
                {
                    ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip")
                }
            },
            "file",
            "board.boardoil.zip");

        var response = await Client.PostAsync("/api/boards/import", requestContent);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>(JsonOptions);
        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
        Assert.NotNull(envelope.Data);
        Assert.Equal("Renamed Board", envelope.Data!.Name);
        Assert.Equal("Imported API Board description", envelope.Data.Description);
        Assert.Equal(["Todo"], envelope.Data.Columns.Select(x => x.Title).ToArray());
        Assert.Equal("Bug", envelope.Data.Columns[0].Cards[0].CardTypeName);
        Assert.Equal(["NeedsReview", "Urgent"], envelope.Data.Columns[0].Cards[0].TagNames);
    }

    [Fact]
    public async Task ImportBoardPackage_WithArchivePayload_ShouldImportArchivedCards()
    {
        var manifest = BoardPackageContract.CreateManifest("0.3.0");
        var payload = new BoardPackageBoardDto(
            "Imported API Archive Board",
            "Imported API archive board description",
            [new BoardPackageCardTypeDto("Story", null, true)],
            [],
            []);
        var archivePayload = new BoardPackageArchiveDto(
            [
                new BoardPackageArchivedCardDto(
                    444,
                    "API imported archived card",
                    ["Urgent"],
                    new DateTime(2026, 04, 20, 12, 0, 0, DateTimeKind.Utc),
                    """{"schema":"archived-card","version":1,"capturedAtUtc":"2026-04-20T12:00:00Z","payload":{"title":"API imported archived card"}}""")
            ]);

        using var requestContent = new MultipartFormDataContent();
        requestContent.Add(
            new ByteArrayContent(BuildBoardPackage(manifest, payload, archivePayload))
            {
                Headers =
                {
                    ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip")
                }
            },
            "file",
            "board.boardoil.zip");

        var response = await Client.PostAsync("/api/boards/import", requestContent);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>(JsonOptions);
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.Data);
        var boardId = envelope.Data!.Id;

        var archivedListEnvelope = await Client.GetFromJsonAsync<ApiEnvelope<ArchivedCardListDto>>(
            $"/api/boards/{boardId}/cards/archived",
            JsonOptions);
        Assert.NotNull(archivedListEnvelope);
        Assert.NotNull(archivedListEnvelope!.Data);
        Assert.Single(archivedListEnvelope.Data!.Items);
        Assert.Equal("API imported archived card", archivedListEnvelope.Data.Items[0].Title);
        Assert.Equal(["Urgent"], archivedListEnvelope.Data.Items[0].TagNames);
    }

    [Fact]
    public async Task ImportBoardPackage_WhenFileIsMissing_ShouldReturnBadRequest()
    {
        using var requestContent = new MultipartFormDataContent();

        var response = await Client.PostAsync("/api/boards/import", requestContent);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.NotNull(payload.ValidationErrors);
        Assert.Contains("file", payload.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task ImportBoardPackage_WhenUploadIsNotZip_ShouldReturnBadRequest()
    {
        using var requestContent = new MultipartFormDataContent();
        requestContent.Add(new ByteArrayContent([0x01, 0x02, 0x03]), "file", "broken.zip");

        var response = await Client.PostAsync("/api/boards/import", requestContent);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>(JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.NotNull(payload.ValidationErrors);
        Assert.Contains("file", payload.ValidationErrors!.Keys);
    }

    private sealed class FakeTasksMdClient : ITasksMdClient
    {
        public TasksMdBoardImportModel Model { get; set; } = new([], []);

        public Task<TasksMdBoardImportModel> LoadBoardAsync(Uri baseUri, CancellationToken cancellationToken = default)
        {
            _ = baseUri;
            _ = cancellationToken;
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

    private sealed record ApiEnvelope<T>(
        bool Success,
        T? Data,
        int StatusCode,
        string? Message,
        Dictionary<string, string[]>? ValidationErrors);
}
