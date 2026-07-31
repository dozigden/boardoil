using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoardOil.Api.Tests.Infrastructure;
using BoardOil.Contracts.Board;
using BoardOil.Services.Board;
using Xunit;

namespace BoardOil.Api.Tests;

public sealed class BoardImportApiIntegrationTests : TestBaseIntegration
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
                        new BoardPackageCardDto("Card A", "Description A", "Bug", ["Urgent", "NeedsReview"], Id: 1)
                    ])
            ],
            NextCardId: 2);

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
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<BoardDto>>(JsonOptions);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
        Assert.Equal(201, envelope.StatusCode);
        Assert.NotNull(envelope.Data);
        Assert.Equal("Renamed Board", envelope.Data!.Name);
        Assert.True(envelope.Data.Id > 0);
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
