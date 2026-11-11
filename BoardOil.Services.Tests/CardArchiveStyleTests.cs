using System.Text.Json;
using BoardOil.Abstractions.Card;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Style;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardArchiveStyleTests : TestBaseDb
{
    [Theory]
    [InlineData("presets", "{\"presetIndex\":7}", "presets", "presets")]
    [InlineData("PRESETS", "{\"presetIndex\":\"7\"}", "presets", "presets")]
    [InlineData("solid", "{\"backgroundColor\":\"#aabbcc\",\"textColorMode\":\"custom\",\"textColor\":\"#112233\",\"borderMode\":\"custom\",\"borderColor\":\"#445566\"}", "solid", "solid")]
    [InlineData("solid", "{\"backgroundColor\":\"#aabbcc\",\"textColorMode\":\"auto\"}", "solid", "solid")]
    [InlineData("gradient", "{\"leftColor\":\"#aabbcc\",\"rightColor\":\"#112233\",\"textColorMode\":\"auto\",\"borderMode\":\"none\"}", "gradient", "presets")]
    [InlineData("auto", "{}", "auto", "presets")]
    [InlineData(null, null, "presets", "presets")]
    [InlineData("solid", null, "presets", "presets")]
    [InlineData("unknown", "{}", "presets", "presets")]
    [InlineData("solid", "not json", "presets", "presets")]
    [InlineData("solid", "{}", "presets", "presets")]
    [InlineData("presets", "{\"presetIndex\":99}", "presets", "presets")]
    public async Task Unarchive_ShouldRecoverSupportedArchivedStylesAndDefaultUnavailableStyles(
        string? styleName, string? properties, string expectedTagStyle, string expectedSlickStyle)
    {
        // Arrange
        var board = CreateBoard().AddColumn("Todo").Build();
        await SeedSnapshotAsync(board.BoardId, styleName, properties);
        DbContextForArrange.Slicks.Add(new EntitySlick
        {
            BoardId = board.BoardId,
            Name = "Occupied preset",
            NormalisedName = "OCCUPIED PRESET",
            StyleName = "presets",
            StylePropertiesJson = """{"presetIndex":0}"""
        });
        await DbContextForArrange.SaveChangesAsync();

        // Act
        var result = await ResolveService<ICardArchiveService>().UnarchiveCardAsync(board.BoardId, 100, ActorUserId);

        // Assert
        Assert.True(result.Success);
        var tag = await DbContextForAssert.Tags.SingleAsync(x => x.BoardId == board.BoardId);
        var slick = await DbContextForAssert.Slicks.SingleAsync(x => x.BoardId == board.BoardId && x.Name == "Archived slick");
        Assert.Equal(expectedTagStyle, tag.StyleName);
        Assert.Equal(expectedSlickStyle, slick.StyleName);
        Assert.Equal("✨", tag.Emoji);
        Assert.Equal(slick.Id, result.Data!.SlickId);
        Assert.Equal(slick.StylePropertiesJson, result.Data.Slick!.StylePropertiesJson);
        AssertArchivedStyleValues(tag.StyleName, tag.StylePropertiesJson, styleName, properties);
        AssertArchivedStyleValues(slick.StyleName, slick.StylePropertiesJson, styleName, properties);
        if (expectedSlickStyle == "presets")
        {
            var preset = Assert.IsType<PresetStyleDefinition>(StyleDefinitionCodec.ParseForWrite(slick.StyleName, slick.StylePropertiesJson).Definition);
            Assert.InRange(preset.PresetIndex, 1, StyleDefinitionCodec.PresetCount - 1);
        }
    }

    [Theory]
    [InlineData("presets", "{\"presetIndex\":7}")]
    [InlineData("unknown", "not json")]
    public async Task Unarchive_WhenDefinitionsExist_ShouldKeepCurrentStylesAndEmojiIgnoringArchivedValues(string archivedStyleName, string archivedProperties)
    {
        // Arrange
        var board = CreateBoard().AddColumn("Todo").Build();
        await SeedSnapshotAsync(board.BoardId, archivedStyleName, archivedProperties);
        var tag = DbContextForArrange.Tags.Add(new EntityTag
        {
            BoardId = board.BoardId,
            Name = "ARCHIVED TAG",
            NormalisedName = "ARCHIVED TAG",
            StyleName = "auto",
            StylePropertiesJson = "{}",
            Emoji = "🚀"
        }).Entity;
        var slick = DbContextForArrange.Slicks.Add(new EntitySlick
        {
            BoardId = board.BoardId,
            Name = "ARCHIVED SLICK",
            NormalisedName = "ARCHIVED SLICK",
            StyleName = "presets",
            StylePropertiesJson = """{"presetIndex":4}"""
        }).Entity;
        await DbContextForArrange.SaveChangesAsync();

        // Act
        var result = await ResolveService<ICardArchiveService>().UnarchiveCardAsync(board.BoardId, 100, ActorUserId);

        // Assert
        Assert.True(result.Success);
        var restoredTag = Assert.Single(result.Data!.Tags);
        Assert.Equal(tag.Id, restoredTag.Id);
        Assert.Equal(tag.Name, restoredTag.Name);
        Assert.Equal(tag.StyleName, restoredTag.StyleName);
        Assert.Equal(tag.StylePropertiesJson, restoredTag.StylePropertiesJson);
        Assert.Equal(tag.Emoji, restoredTag.Emoji);
        Assert.Equal(slick.Id, result.Data.SlickId);
        Assert.Equal(slick.StylePropertiesJson, result.Data.Slick!.StylePropertiesJson);
        Assert.Single(await DbContextForAssert.Tags.Where(x => x.BoardId == board.BoardId).ToListAsync());
        Assert.Single(await DbContextForAssert.Slicks.Where(x => x.BoardId == board.BoardId).ToListAsync());
    }

    [Fact]
    public async Task Unarchive_WhenOldSnapshotOmitsStyleMetadata_ShouldRestoreWithDefaults()
    {
        // Arrange
        var board = CreateBoard().AddColumn("Todo").Build();
        await SeedSnapshotAsync(board.BoardId, null, null, includeStyleMetadata: false);

        // Act
        var result = await ResolveService<ICardArchiveService>().UnarchiveCardAsync(board.BoardId, 100, ActorUserId);

        // Assert
        Assert.True(result.Success);
        var tag = Assert.Single(result.Data!.Tags);
        Assert.Equal("presets", tag.StyleName);
        Assert.Null(tag.Emoji);
        Assert.Equal("presets", result.Data.Slick!.StyleName);
        Assert.True(StyleDefinitionCodec.ParseForWrite(tag.StyleName, tag.StylePropertiesJson).IsValid);
        Assert.True(StyleDefinitionCodec.ParseForWrite(result.Data.Slick.StyleName, result.Data.Slick.StylePropertiesJson).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("[{\"name\":null}]")]
    public async Task Unarchive_WhenTagMetadataIsUnavailable_ShouldRestoreTagByNameWithDefaults(string? tagsJson)
    {
        // Arrange
        var board = CreateBoard().AddColumn("Todo").Build();
        await SeedSnapshotAsync(board.BoardId, null, null, includeStyleMetadata: false);
        var archive = await DbContextForArrange.Set<EntityArchivedCard>().SingleAsync();
        var json = System.Text.Json.Nodes.JsonNode.Parse(archive.SnapshotJson)!;
        var payload = json["payload"]!.AsObject();
        if (tagsJson is null)
        {
            payload.Remove("tags");
        }
        else
        {
            payload["tags"] = System.Text.Json.Nodes.JsonNode.Parse(tagsJson);
        }
        archive.SnapshotJson = json.ToJsonString();
        await DbContextForArrange.SaveChangesAsync();

        // Act
        var result = await ResolveService<ICardArchiveService>().UnarchiveCardAsync(board.BoardId, 100, ActorUserId);

        // Assert
        Assert.True(result.Success);
        var tag = Assert.Single(result.Data!.Tags);
        Assert.Equal("Archived tag", tag.Name);
        Assert.Equal("presets", tag.StyleName);
        Assert.Null(tag.Emoji);
        Assert.True(StyleDefinitionCodec.ParseForWrite(tag.StyleName, tag.StylePropertiesJson).IsValid);
        var restoredCard = await DbContextForAssert.Cards.Include(x => x.CardTags)
            .SingleAsync(x => x.BoardId == board.BoardId && x.BoardCardId == 100);
        Assert.Equal(tag.Id, Assert.Single(restoredCard.CardTags).TagId);
        Assert.Empty(await DbContextForAssert.Set<EntityArchivedCard>().ToListAsync());
    }

    private async Task SeedSnapshotAsync(int boardId, string? styleName, string? properties, bool includeStyleMetadata = true)
    {
        var tagsJson = "[]";
        var slickStyleFields = string.Empty;
        if (includeStyleMetadata)
        {
            tagsJson = $$"""
                [{"id":99999,"name":" ARCHIVED TAG ","styleName":{{JsonSerializer.Serialize(styleName)}},"stylePropertiesJson":{{JsonSerializer.Serialize(properties)}},"emoji":"✨"}]
                """;
            slickStyleFields = $$"""
                ,"slickStyleName":{{JsonSerializer.Serialize(styleName)}},"slickStylePropertiesJson":{{JsonSerializer.Serialize(properties)}}
                """;
        }

        // Explicit V1 JSON exercises the additive reader contract independently of the current writer.
        DbContextForArrange.Set<EntityArchivedCard>().Add(new EntityArchivedCard
        {
            BoardId = boardId,
            OriginalCardId = 100,
            ArchivedAtUtc = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc),
            SearchTitle = "Archived styles",
            SearchTagsJson = "[\"Archived tag\"]",
            SnapshotJson = $$"""
                {"schema":"archived-card","version":1,"capturedAtUtc":"2026-11-11T12:00:00Z","payload":{
                    "boardId":{{boardId}},"originalCardId":100,"boardColumnId":0,"originalColumnName":"Todo",
                    "cardTypeId":0,"cardTypeName":"Story","cardTypeEmoji":null,"title":"Archived styles",
                    "description":"Desc","sortKey":"A","tags":{{tagsJson}},"tagNames":["Archived tag"],
                    "createdAtUtc":"2026-11-10T12:00:00Z","updatedAtUtc":"2026-11-10T12:00:00Z",
                    "slickId":99999,"slickName":"Archived slick"{{slickStyleFields}}
                }
                }
                """
        });
        await DbContextForArrange.SaveChangesAsync();
    }

    private static void AssertArchivedStyleValues(string restoredName, string restoredJson, string? archivedName, string? archivedJson)
    {
        var parsed = StyleDefinitionCodec.ParseForWrite(restoredName, restoredJson);
        Assert.True(parsed.IsValid);
        if (parsed.Definition is SolidStyleDefinition solid)
        {
            Assert.Equal("#AABBCC", solid.BackgroundColour);
            if (archivedJson!.Contains("custom", StringComparison.Ordinal))
            {
                Assert.Equal(new StyleManualOptions(StyleTextColourMode.Custom, "#112233", StyleBorderMode.Custom, "#445566"), solid.ManualOptions);
            }
            else
            {
                Assert.Equal(new StyleManualOptions(StyleTextColourMode.Auto, null, StyleBorderMode.Auto, null), solid.ManualOptions);
            }
        }
        if (parsed.Definition is GradientStyleDefinition gradient)
        {
            Assert.Equal("#AABBCC", gradient.LeftColour);
            Assert.Equal("#112233", gradient.RightColour);
            Assert.Equal(StyleBorderMode.None, gradient.ManualOptions.BorderMode);
        }
        if (parsed.Definition is PresetStyleDefinition preset && archivedName?.ToUpperInvariant() == "PRESETS" && archivedJson!.Contains('7'))
        {
            Assert.Equal(7, preset.PresetIndex);
        }
    }
}
