using System.IO.Compression;
using BoardOil.Abstractions.Board;
using BoardOil.Contracts.Board;
using BoardOil.Services.Board;
using BoardOil.Services.Tests.Infrastructure;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class BoardImportServiceV1Tests : TestBaseDb
{
    [Fact]
    public async Task ImportBoardPackageAsync_WhenSchemaVersionIsOneRawPayload_ShouldImportWithEmptyDescription()
    {
        const string manifestJson =
            """
            {
              "format": "boardoil-board-package",
              "schemaVersion": 1,
              "exportedByVersion": "0.2.0",
              "entries": [
                { "kind": "board", "path": "board.json" }
              ]
            }
            """;
        const string boardPayloadV1Json =
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
            new ImportBoardPackageRequest(null, BuildBoardPackageWithRawEntries(manifestJson, boardPayloadV1Json)),
            ActorUserId);

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("Legacy Board", result.Data!.Name);
        Assert.Equal(string.Empty, result.Data.Description);
    }

    private static byte[] BuildBoardPackageWithRawEntries(string manifestJson, string boardJson)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteRawEntry(archive, BoardPackageContract.ManifestPath, manifestJson);
            WriteRawEntry(archive, BoardPackageContract.BoardEntryPath, boardJson);
        }

        return stream.ToArray();
    }

    private static void WriteRawEntry(ZipArchive archive, string path, string payload)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(payload);
    }
}
