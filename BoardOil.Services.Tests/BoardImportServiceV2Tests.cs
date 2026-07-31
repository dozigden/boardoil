using System.IO.Compression;
using BoardOil.Abstractions.Board;
using BoardOil.Contracts.Board;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Board;
using BoardOil.Services.Tag;
using BoardOil.Services.Tests.Infrastructure;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class BoardImportServiceV2Tests : TestBaseDb
{
    [Fact]
    public async Task ImportBoardPackageAsync_WhenFrozenSchemaTwoExportIsImported_ShouldPreserveCompletePackage()
    {
        const string manifestJson =
            """
            {
              "format": "boardoil-board-package",
              "schemaVersion": 2,
              "exportedByVersion": "0.3.0",
              "entries": [
                { "kind": "board", "path": "board.json" },
                { "kind": "archive", "path": "archive.json" }
              ]
            }
            """;
        const string boardPayloadJson =
            """
            {
              "name": "Frozen V2 Board",
              "description": "Complete schema two export fixture",
              "cardTypes": [
                {
                  "name": "Story",
                  "emoji": null,
                  "isSystem": true,
                  "styleName": "solid",
                  "stylePropertiesJson": "{\"backgroundColor\":\"#FFFFFF\",\"textColorMode\":\"auto\"}"
                },
                {
                  "name": "Bug",
                  "emoji": "🐞",
                  "isSystem": false,
                  "styleName": "gradient",
                  "stylePropertiesJson": "{\"leftColor\":\"#F6D32D\",\"rightColor\":\"#C64600\",\"textColorMode\":\"auto\"}"
                }
              ],
              "tags": [
                {
                  "name": "Urgent",
                  "styleName": "solid",
                  "stylePropertiesJson": "{\"backgroundColor\":\"#ED333B\",\"textColorMode\":\"auto\"}",
                  "emoji": "🟥"
                }
              ],
              "columns": [
                {
                  "title": "Todo",
                  "cards": [
                    {
                      "title": "Fix legacy import",
                      "description": "Keep every supported V2 field",
                      "cardTypeName": "Bug",
                      "tagNames": ["Urgent", "Ad hoc"],
                      "assignedUserEmail": "ACTOR@LOCALHOST",
                      "comments": [
                        {
                          "text": "  Authored legacy comment  ",
                          "postedAtUtc": "2026-04-19T08:00:00Z",
                          "authorEmail": "ACTOR@LOCALHOST"
                        },
                        {
                          "text": "Unknown legacy author",
                          "postedAtUtc": "2026-04-19T08:01:00Z",
                          "authorEmail": "missing-user@example.com"
                        }
                      ],
                      "slickName": "Release train",
                      "externalUrl": "https://github.com/example/repository/issues/42"
                    }
                  ]
                },
                {
                  "title": "Done",
                  "cards": [
                    {
                      "title": "Shipped legacy card",
                      "description": "Already complete",
                      "cardTypeName": "Story",
                      "tagNames": []
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
              ],
              "slickCohesionModeEnabled": false
            }
            """;
        const string archivePayloadJson =
            """
            {
              "cards": [
                {
                  "originalCardId": 17,
                  "title": "Archived legacy card",
                  "tagNames": ["Urgent"],
                  "archivedAtUtc": "2026-04-20T09:00:00Z",
                  "snapshotJson": "{\"schema\":\"archived-card\",\"version\":1,\"capturedAtUtc\":\"2026-04-20T09:00:00Z\",\"payload\":{\"originalCardId\":17,\"title\":\"Archived legacy card\"}}"
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
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        var boardId = result.Data!.Id;
        Assert.Equal("Frozen V2 Board", result.Data.Name);
        Assert.Equal("Complete schema two export fixture", result.Data.Description);
        Assert.False(result.Data.SlickCohesionModeEnabled);
        Assert.Equal(["Todo", "Done"], result.Data.Columns.Select(x => x.Title).ToArray());

        var ownerMembership = DbContextForAssert.BoardMembers.Single(x => x.BoardId == boardId && x.UserId == ActorUserId);
        Assert.Equal(BoardMemberRole.Owner, ownerMembership.Role);

        var cardTypes = DbContextForAssert.CardTypes
            .Where(x => x.BoardId == boardId)
            .OrderBy(x => x.Name)
            .ToList();
        Assert.Equal(["Bug", "Story"], cardTypes.Select(x => x.Name).ToArray());
        Assert.Contains(
            cardTypes,
            x => x.Name == "Bug"
                && x.Emoji == "🐞"
                && !x.IsSystem
                && x.StyleName == "gradient"
                && x.StylePropertiesJson == """{"leftColor":"#F6D32D","rightColor":"#C64600","textColorMode":"auto"}""");
        Assert.Contains(
            cardTypes,
            x => x.Name == "Story"
                && x.IsSystem
                && x.StyleName == "solid"
                && x.StylePropertiesJson == """{"backgroundColor":"#FFFFFF","textColorMode":"auto"}""");

        var tags = DbContextForAssert.Tags
            .Where(x => x.BoardId == boardId)
            .OrderBy(x => x.Name)
            .ToList();
        Assert.Equal(["Ad hoc", "Urgent"], tags.Select(x => x.Name).ToArray());
        Assert.Contains(
            tags,
            x => x.Name == "Urgent"
                && x.Emoji == "🟥"
                && x.StyleName == "solid"
                && x.StylePropertiesJson == """{"backgroundColor":"#ED333B","textColorMode":"auto"}""");
        Assert.Contains(tags, x => x.Name == "Ad hoc" && x.StyleName == TagStyleSchemaValidator.PresetsStyleName);

        var slick = DbContextForAssert.Slicks.Single(x => x.BoardId == boardId);
        Assert.Equal("Release train", slick.Name);
        Assert.Equal("RELEASE TRAIN", slick.NormalisedName);
        Assert.Equal("solid", slick.StyleName);
        Assert.Equal("""{"backgroundColor":"#2E8B57","textColorMode":"auto"}""", slick.StylePropertiesJson);

        var activeCards = DbContextForAssert.Cards
            .Where(x => x.BoardId == boardId)
            .OrderBy(x => x.BoardCardId)
            .ToList();
        Assert.Equal(2, activeCards.Count);
        var fixCard = activeCards[0];
        Assert.Equal(1, fixCard.BoardCardId);
        Assert.Equal("Fix legacy import", fixCard.Title);
        Assert.Equal("Keep every supported V2 field", fixCard.Description);
        Assert.Equal("Bug", fixCard.CardType.Name);
        Assert.Equal(ActorUserId, fixCard.AssignedUserId);
        Assert.Equal(slick.Id, fixCard.SlickId);
        Assert.Equal("https://github.com/example/repository/issues/42", fixCard.ExternalUrl);
        Assert.Equal(2, activeCards[1].BoardCardId);
        Assert.Equal("Shipped legacy card", activeCards[1].Title);
        Assert.Equal("Story", activeCards[1].CardType.Name);

        var fixCardTags = DbContextForAssert.CardTags
            .Where(x => x.CardId == fixCard.Id)
            .Select(x => x.Tag.Name)
            .OrderBy(x => x)
            .ToList();
        Assert.Equal(["Ad hoc", "Urgent"], fixCardTags);

        var comments = DbContextForAssert.CardComments
            .Where(x => x.CardId == fixCard.Id)
            .OrderBy(x => x.PostedAtUtc)
            .ToList();
        Assert.Equal(2, comments.Count);
        Assert.Equal("Authored legacy comment", comments[0].Text);
        Assert.Equal(ActorUserId, comments[0].AuthorUserId);
        Assert.Equal(new DateTime(2026, 4, 19, 8, 0, 0, DateTimeKind.Utc), comments[0].PostedAtUtc);
        Assert.Equal("Unknown legacy author", comments[1].Text);
        Assert.Null(comments[1].AuthorUserId);

        var archivedCard = DbContextForAssert.ArchivedCards.Single(x => x.BoardId == boardId);
        Assert.Equal(17, archivedCard.OriginalCardId);
        Assert.Equal("Archived legacy card", archivedCard.SearchTitle);
        Assert.Equal("""["Urgent"]""", archivedCard.SearchTagsJson);
        Assert.Equal(new DateTime(2026, 4, 20, 9, 0, 0, DateTimeKind.Utc), archivedCard.ArchivedAtUtc);
        Assert.Equal(
            """{"schema":"archived-card","version":1,"capturedAtUtc":"2026-04-20T09:00:00Z","payload":{"originalCardId":17,"title":"Archived legacy card"}}""",
            archivedCard.SnapshotJson);

        var nextCardId = DbContextForAssert.BoardCardIdSequences
            .Where(x => x.BoardId == boardId)
            .Select(x => x.NextCardId)
            .Single();
        Assert.Equal(18, nextCardId);
    }

    private static byte[] BuildBoardPackageWithRawEntries(
        string manifestJson,
        string boardJson,
        string archiveJson)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteRawEntry(archive, BoardPackageContract.ManifestPath, manifestJson);
            WriteRawEntry(archive, BoardPackageContract.BoardEntryPath, boardJson);
            WriteRawEntry(archive, BoardPackageContract.ArchiveEntryPath, archiveJson);
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
