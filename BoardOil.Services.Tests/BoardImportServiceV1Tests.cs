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
        Assert.True(result.Data.SlickCohesionModeEnabled);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    // Schema 1 and 2 intentionally share board payload compatibility until version 3 introduces divergence.
    public async Task ImportBoardPackageAsync_WhenSchemaVersionIsOneOrTwo_WithSlickPayload_ShouldImportSlicksAndMembership(int schemaVersion)
    {
        var manifestJson = CreateManifestJson(schemaVersion);
        const string boardPayloadJson =
            """
            {
              "name": "Legacy Slick Board",
              "cardTypes": [
                { "name": "Story", "emoji": null, "isSystem": true }
              ],
              "tags": [],
              "columns": [
                {
                  "title": "Todo",
                  "cards": [
                    {
                      "title": "Card A",
                      "description": "Description",
                      "cardTypeName": "Story",
                      "tagNames": [],
                      "slickName": "Release train",
                      "externalUrl": "https://github.com/example/repository"
                    }
                  ]
                }
              ],
              "slicks": [
                {
                  "name": "Release train",
                  "styleName": "solid",
                  "stylePropertiesJson": "{\"backgroundColor\":\"#2E8B57\",\"textColorMode\":\"auto\"}"
                }
              ]
            }
            """;

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(null, BuildBoardPackageWithRawEntries(manifestJson, boardPayloadJson)),
            ActorUserId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var boardId = result.Data!.Id;
        Assert.True(result.Data.SlickCohesionModeEnabled);

        var slick = DbContextForAssert.Slicks.Single(x => x.BoardId == boardId);
        Assert.Equal("Release train", slick.Name);
        Assert.Equal("RELEASE TRAIN", slick.NormalisedName);
        Assert.Equal("solid", slick.StyleName);

        var importedCard = DbContextForAssert.Cards.Single(x => x.BoardColumn.BoardId == boardId && x.Title == "Card A");
        Assert.Equal(slick.Id, importedCard.SlickId);
        Assert.Equal("https://github.com/example/repository", importedCard.ExternalUrl);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ImportBoardPackageAsync_WhenLegacyRawPayloadHasArchiveIdentityConflict_ShouldAllocateDeterministically(int schemaVersion)
    {
        var manifestJson = CreateManifestJson(schemaVersion, includeArchive: true);
        const string boardPayloadJson =
            """
            {
              "name": "Legacy Identity Board",
              "cardTypes": [
                { "name": "Story", "emoji": null, "isSystem": true }
              ],
              "tags": [],
              "columns": [
                {
                  "title": "Todo",
                  "cards": [
                    {
                      "title": "Legacy active card",
                      "description": "Description",
                      "cardTypeName": "Story",
                      "tagNames": []
                    }
                  ]
                }
              ]
            }
            """;
        const string archivePayloadJson =
            """
            {
              "cards": [
                {
                  "originalCardId": 1,
                  "title": "Legacy archived card",
                  "tagNames": [],
                  "archivedAtUtc": "2026-04-20T09:00:00Z",
                  "snapshotJson": "{}"
                }
              ]
            }
            """;

        var service = ResolveService<IBoardPackageImportService>();
        var result = await service.ImportBoardPackageAsync(
            new ImportBoardPackageRequest(
                null,
                BuildBoardPackageWithRawEntries(manifestJson, boardPayloadJson, archivePayloadJson)),
            ActorUserId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var boardId = result.Data!.Id;
        var activeCardId = DbContextForAssert.Cards
            .Where(x => x.BoardId == boardId)
            .Select(x => x.BoardCardId)
            .Single();
        var archivedCardId = DbContextForAssert.ArchivedCards
            .Where(x => x.BoardId == boardId)
            .Select(x => x.OriginalCardId)
            .Single();
        var nextCardId = DbContextForAssert.BoardCardIdSequences
            .Where(x => x.BoardId == boardId)
            .Select(x => x.NextCardId)
            .Single();
        Assert.Equal(2, activeCardId);
        Assert.Equal(1, archivedCardId);
        Assert.Equal(3, nextCardId);
    }

    private static string CreateManifestJson(int schemaVersion, bool includeArchive = false)
    {
        var archiveEntry = includeArchive
            ? ",\n    { \"kind\": \"archive\", \"path\": \"archive.json\" }"
            : string.Empty;
        return $$"""
                 {
                   "format": "boardoil-board-package",
                   "schemaVersion": {{schemaVersion}},
                   "exportedByVersion": "0.2.0",
                   "entries": [
                     { "kind": "board", "path": "board.json" }{{archiveEntry}}
                   ]
                 }
                 """;
    }

    private static byte[] BuildBoardPackageWithRawEntries(
        string manifestJson,
        string boardJson,
        string? archiveJson = null)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteRawEntry(archive, BoardPackageContract.ManifestPath, manifestJson);
            WriteRawEntry(archive, BoardPackageContract.BoardEntryPath, boardJson);
            if (archiveJson is not null)
            {
                WriteRawEntry(archive, BoardPackageContract.ArchiveEntryPath, archiveJson);
            }
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
