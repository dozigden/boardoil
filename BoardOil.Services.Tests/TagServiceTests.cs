using BoardOil.Abstractions;
using BoardOil.Abstractions.Tag;
using BoardOil.Contracts.Tag;
using BoardOil.Services.Tag;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;
using CardTagEntity = BoardOil.Data.Abstractions.Entities.EntityCardTag;
using TagEntity = BoardOil.Data.Abstractions.Entities.EntityTag;

namespace BoardOil.Services.Tests;

public sealed class TagServiceTests : TestBaseDb
{
    [Fact]
    public async Task CreateTagAsync_WhenTagMissing_ShouldCreateTagWithDefaultStyle()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        var service = CreateService();

        // Act
        var result = await service.CreateTagAsync(boardId, new CreateTagRequest("Bug"), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("Bug", result.Data!.Name);
        Assert.Equal("presets", result.Data.StyleName);

        var stored = await DbContextForAssert.Tags.SingleAsync();
        Assert.Equal(boardId, stored.BoardId);
        Assert.Equal("Bug", stored.Name);
        Assert.Equal("BUG", stored.NormalisedName);
        Assert.Equal("presets", stored.StyleName);
        Assert.NotEmpty(stored.StylePropertiesJson);

        using var styleProperties = JsonDocument.Parse(stored.StylePropertiesJson);
        Assert.True(styleProperties.RootElement.TryGetProperty("presetIndex", out var presetIndex));
        Assert.InRange(presetIndex.GetInt32(), 0, 11);
        Assert.False(styleProperties.RootElement.TryGetProperty("textColorMode", out _));
        Assert.Equal([boardId], ResolveBoardEvents().ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task CreateTagAsync_WhenPresetUnused_ShouldPreferUnusedPreset()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        SeedPresetTags(boardId, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        await DbContextForArrange.SaveChangesAsync();
        var service = CreateService();

        // Act
        var result = await service.CreateTagAsync(boardId, new CreateTagRequest("Bug"), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("presets", result.Data!.StyleName);
        Assert.Equal(11, ReadPresetIndex(result.Data.StylePropertiesJson));
    }

    [Fact]
    public async Task CreateTagAsync_WhenEmojiProvided_ShouldPersistEmoji()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        var service = CreateService();

        // Act
        var result = await service.CreateTagAsync(boardId, new CreateTagRequest("Bug", "🐞"), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("🐞", result.Data!.Emoji);

        var stored = await DbContextForAssert.Tags.SingleAsync();
        Assert.Equal("🐞", stored.Emoji);
    }

    [Fact]
    public async Task CreateTagDefinitionAsync_WithExplicitStyle_ShouldPersistCanonicalDefinition()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        var service = CreateService();
        var definition = new TagDefinitionCreate(
            "Feature",
            "🎬️",
            new TagStylePatch(
                "gradient",
                """{"leftColor":"#99c1f1","rightColor":"#3584e4","textColorMode":"auto","borderMode":"none"}"""));

        // Act
        var result = await service.CreateTagDefinitionAsync(boardId, definition, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("Feature", result.Data!.Name);
        Assert.Equal("🎬️", result.Data.Emoji);
        Assert.Equal("gradient", result.Data.StyleName);
        Assert.Equal(
            """{"leftColor":"#99C1F1","rightColor":"#3584E4","textColorMode":"auto","borderMode":"none"}""",
            result.Data.StylePropertiesJson);

        var stored = await DbContextForAssert.Tags.SingleAsync();
        Assert.Equal(result.Data.StylePropertiesJson, stored.StylePropertiesJson);
        Assert.Equal([boardId], ResolveBoardEvents().ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task CreateTagAsync_WhenTagAlreadyExists_ShouldReturnExistingTag()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        DbContextForArrange.Tags.Add(new TagEntity
        {
            BoardId = boardId,
            Name = "Bug",
            NormalisedName = "BUG",
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#114488","textColorMode":"auto"}""",
        });
        await DbContextForArrange.SaveChangesAsync();

        var service = CreateService();

        // Act
        var result = await service.CreateTagAsync(boardId, new CreateTagRequest("Bug"), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("Bug", result.Data!.Name);
        Assert.Equal(1, await DbContextForAssert.Tags.CountAsync());
        Assert.Empty(ResolveBoardEvents().ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task CreateTagAsync_WhenNameContainsComma_ShouldReturnValidationError()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        var service = CreateService();

        // Act
        var result = await service.CreateTagAsync(boardId, new CreateTagRequest("Bug,Urgent"), ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("name"));
    }

    [Fact]
    public async Task GetTagsAsync_ShouldReturnAllTags()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        var otherBoardId = CreateBoard("Other Board")
            .AddColumn("Todo")
            .Build()
            .BoardId;

        DbContextForArrange.Tags.AddRange(
            new TagEntity
            {
                BoardId = boardId,
                Name = "Bug",
                NormalisedName = "BUG",
                StyleName = "solid",
                StylePropertiesJson = """{"backgroundColor":"#114488","textColorMode":"auto"}""",
            },
            new TagEntity
            {
                BoardId = boardId,
                Name = "Urgent",
                NormalisedName = "URGENT",
                StyleName = "solid",
                StylePropertiesJson = """{"backgroundColor":"#AA3322","textColorMode":"auto"}""",
            },
            new TagEntity
            {
                BoardId = otherBoardId,
                Name = "Other",
                NormalisedName = "OTHER",
                StyleName = "solid",
                StylePropertiesJson = """{"backgroundColor":"#117733","textColorMode":"auto"}""",
            });
        await DbContextForArrange.SaveChangesAsync();

        // Act
        var service = CreateService();
        var result = await service.GetTagsAsync(boardId, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(["Bug", "Urgent"], result.Data!.Select(x => x.Name).ToArray());
        Assert.Equal(
            """{"backgroundColor":"#114488","textColorMode":"auto"}""",
            result.Data.Single(x => x.Name == "Bug").StylePropertiesJson);
    }

    [Fact]
    public async Task UpdateTagStyleAsync_WhenStyleShapeIsInvalid_ShouldReturnValidationError()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;

        DbContextForArrange.Tags.Add(new TagEntity
        {
            BoardId = boardId,
            Name = "Bug",
            NormalisedName = "BUG",
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#114488","textColorMode":"auto"}""",
        });
        await DbContextForArrange.SaveChangesAsync();
        var tagId = await DbContextForArrange.Tags.Select(x => x.Id).SingleAsync();

        // Act
        var service = CreateService();
        var result = await service.UpdateTagStyleAsync(boardId, tagId, new UpdateTagRequest(
            Name: "Bug",
            StyleName: "solid",
            StylePropertiesJson: """{"backgroundColor":"blue","textColorMode":"auto","borderMode":"auto"}"""), ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("stylePropertiesJson"));
    }

    [Fact]
    public async Task UpdateTagStyleAsync_WhenStyleJsonIsNotObject_ShouldReturnValidationError()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;

        DbContextForArrange.Tags.Add(new TagEntity
        {
            BoardId = boardId,
            Name = "Bug",
            NormalisedName = "BUG",
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#114488","textColorMode":"auto"}""",
        });
        await DbContextForArrange.SaveChangesAsync();
        var tagId = await DbContextForArrange.Tags.Select(x => x.Id).SingleAsync();

        // Act
        var service = CreateService();
        var result = await service.UpdateTagStyleAsync(boardId, tagId, new UpdateTagRequest(
            Name: "Bug",
            StyleName: "solid",
            StylePropertiesJson: """["not-an-object"]"""), ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("stylePropertiesJson"));
    }

    [Fact]
    public async Task UpdateTagStyleAsync_WhenPresetsStyleValid_ShouldPersistPresetIndex()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;

        DbContextForArrange.Tags.Add(new TagEntity
        {
            BoardId = boardId,
            Name = "Bug",
            NormalisedName = "BUG",
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#114488","textColorMode":"auto"}""",
        });
        await DbContextForArrange.SaveChangesAsync();
        var tagId = await DbContextForArrange.Tags.Select(x => x.Id).SingleAsync();

        // Act
        var service = CreateService();
        var result = await service.UpdateTagStyleAsync(boardId, tagId, new UpdateTagRequest(
            Name: "Bug",
            StyleName: "presets",
            StylePropertiesJson: """{"presetIndex":3,"textColorMode":"auto","borderMode":"auto"}"""), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("presets", result.Data!.StyleName);
        Assert.Equal("""{"presetIndex":3}""", result.Data.StylePropertiesJson);
    }

    [Fact]
    public async Task UpdateTagStyleAsync_WhenTagExists_ShouldPersistUpdatedStyle()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;

        DbContextForArrange.Tags.Add(new TagEntity
        {
            BoardId = boardId,
            Name = "Bug",
            NormalisedName = "BUG",
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#114488","textColorMode":"auto"}""",
        });
        await DbContextForArrange.SaveChangesAsync();
        var tagId = await DbContextForArrange.Tags.Select(x => x.Id).SingleAsync();

        var updatedStylePropertiesJson = """{"leftColor":"#113355","rightColor":"#557799","textColorMode":"custom","borderMode":"auto","textColor":"#FFFFFF"}""";

        // Act
        var service = CreateService();
        var result = await service.UpdateTagStyleAsync(boardId, tagId, new UpdateTagRequest(
            Name: "Bug",
            StyleName: "gradient",
            StylePropertiesJson: updatedStylePropertiesJson), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("gradient", result.Data!.StyleName);
        Assert.Equal(updatedStylePropertiesJson, result.Data.StylePropertiesJson);

        var stored = await DbContextForAssert.Tags.SingleAsync();
        Assert.Equal("gradient", stored.StyleName);
        Assert.Equal(updatedStylePropertiesJson, stored.StylePropertiesJson);
        Assert.Equal([boardId], ResolveBoardEvents().ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task UpdateTagStyleAsync_WhenNameProvided_ShouldRenameTag()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;

        DbContextForArrange.Tags.Add(new TagEntity
        {
            BoardId = boardId,
            Name = "Bug",
            NormalisedName = "BUG",
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#114488","textColorMode":"auto"}""",
        });
        await DbContextForArrange.SaveChangesAsync();
        var tagId = await DbContextForArrange.Tags.Select(x => x.Id).SingleAsync();

        // Act
        var service = CreateService();
        var result = await service.UpdateTagStyleAsync(boardId, tagId, new UpdateTagRequest(
            StyleName: "solid",
            StylePropertiesJson: """{"backgroundColor":"#114488","textColorMode":"auto","borderMode":"auto"}""",
            Name: "Platform"), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Platform", result.Data!.Name);

        var stored = await DbContextForAssert.Tags.SingleAsync();
        Assert.Equal("Platform", stored.Name);
        Assert.Equal("PLATFORM", stored.NormalisedName);
    }

    [Fact]
    public async Task UpdateTagStyleAsync_WhenNameConflicts_ShouldReturnValidationError()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;

        DbContextForArrange.Tags.AddRange(
            new TagEntity
            {
                BoardId = boardId,
                Name = "Bug",
                NormalisedName = "BUG",
                StyleName = "solid",
                StylePropertiesJson = """{"backgroundColor":"#114488","textColorMode":"auto"}""",
            },
            new TagEntity
            {
                BoardId = boardId,
                Name = "Urgent",
                NormalisedName = "URGENT",
                StyleName = "solid",
                StylePropertiesJson = """{"backgroundColor":"#223344","textColorMode":"auto"}""",
            });
        await DbContextForArrange.SaveChangesAsync();

        var bugTagId = await DbContextForArrange.Tags
            .Where(x => x.NormalisedName == "BUG")
            .Select(x => x.Id)
            .SingleAsync();

        // Act
        var service = CreateService();
        var result = await service.UpdateTagStyleAsync(boardId, bugTagId, new UpdateTagRequest(
            StyleName: "solid",
            StylePropertiesJson: """{"backgroundColor":"#114488","textColorMode":"auto","borderMode":"auto"}""",
            Name: "Urgent"), ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("name"));
    }

    [Fact]
    public async Task UpdateTagStyleAsync_WhenEmojiInvalid_ShouldReturnValidationError()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;

        DbContextForArrange.Tags.Add(new TagEntity
        {
            BoardId = boardId,
            Name = "Bug",
            NormalisedName = "BUG",
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#114488","textColorMode":"auto"}""",
        });
        await DbContextForArrange.SaveChangesAsync();
        var tagId = await DbContextForArrange.Tags.Select(x => x.Id).SingleAsync();

        // Act
        var service = CreateService();
        var result = await service.UpdateTagStyleAsync(boardId, tagId, new UpdateTagRequest(
            Name: "Bug",
            StyleName: "solid",
            StylePropertiesJson: """{"backgroundColor":"#114488","textColorMode":"auto","borderMode":"auto"}""",
            Emoji: "not-emoji"), ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("emoji"));
    }

    [Fact]
    public async Task UpdateTagStyleAsync_WhenEmojiProvided_ShouldPersistOrClearEmoji()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;

        DbContextForArrange.Tags.Add(new TagEntity
        {
            BoardId = boardId,
            Name = "Bug",
            NormalisedName = "BUG",
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#114488","textColorMode":"auto"}""",
            Emoji = "🔥",
        });
        await DbContextForArrange.SaveChangesAsync();
        var tagId = await DbContextForArrange.Tags.Select(x => x.Id).SingleAsync();
        var service = CreateService();

        // Act
        var setEmojiResult = await service.UpdateTagStyleAsync(boardId, tagId, new UpdateTagRequest(
            Name: "Bug",
            StyleName: "solid",
            StylePropertiesJson: """{"backgroundColor":"#114488","textColorMode":"auto","borderMode":"auto"}""",
            Emoji: "⚠️"), ActorUserId);
        var clearEmojiResult = await service.UpdateTagStyleAsync(boardId, tagId, new UpdateTagRequest(
            Name: "Bug",
            StyleName: "solid",
            StylePropertiesJson: """{"backgroundColor":"#114488","textColorMode":"auto","borderMode":"auto"}""",
            Emoji: "   "), ActorUserId);

        // Assert
        Assert.True(setEmojiResult.Success);
        Assert.NotNull(setEmojiResult.Data);
        Assert.Equal("⚠️", setEmojiResult.Data!.Emoji);

        Assert.True(clearEmojiResult.Success);
        Assert.NotNull(clearEmojiResult.Data);
        Assert.Null(clearEmojiResult.Data!.Emoji);

        var stored = await DbContextForAssert.Tags.SingleAsync();
        Assert.Null(stored.Emoji);
    }

    [Fact]
    public async Task UpdateTagStyleAsync_WhenEmojiOmitted_ShouldClearExistingEmoji()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;

        DbContextForArrange.Tags.Add(new TagEntity
        {
            BoardId = boardId,
            Name = "Bug",
            NormalisedName = "BUG",
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#114488","textColorMode":"auto"}""",
            Emoji = "🔥",
        });
        await DbContextForArrange.SaveChangesAsync();
        var tagId = await DbContextForArrange.Tags.Select(x => x.Id).SingleAsync();

        // Act
        var service = CreateService();
        var result = await service.UpdateTagStyleAsync(boardId, tagId, new UpdateTagRequest(
            Name: "Bug",
            StyleName: "gradient",
            StylePropertiesJson: """{"leftColor":"#113355","rightColor":"#557799","textColorMode":"auto","borderMode":"auto"}"""), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Null(result.Data!.Emoji);

        var stored = await DbContextForAssert.Tags.SingleAsync();
        Assert.Null(stored.Emoji);
    }

    [Fact]
    public async Task UpdateTagStyleAsync_WhenSameNamedTagExistsOnAnotherBoard_ShouldNotAffectOtherBoardTag()
    {
        // Arrange
        var firstBoardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        var secondBoardId = CreateBoard("Operations")
            .AddColumn("Todo")
            .Build()
            .BoardId;

        DbContextForArrange.Tags.AddRange(
            new TagEntity
            {
                BoardId = firstBoardId,
                Name = "Bug",
                NormalisedName = "BUG",
                StyleName = "solid",
                StylePropertiesJson = """{"backgroundColor":"#114488","textColorMode":"auto"}""",
            },
            new TagEntity
            {
                BoardId = secondBoardId,
                Name = "Bug",
                NormalisedName = "BUG",
                StyleName = "solid",
                StylePropertiesJson = """{"backgroundColor":"#553311","textColorMode":"auto"}""",
            });
        await DbContextForArrange.SaveChangesAsync();

        var firstBoardTagId = await DbContextForArrange.Tags
            .Where(x => x.BoardId == firstBoardId && x.NormalisedName == "BUG")
            .Select(x => x.Id)
            .SingleAsync();

        var secondBoardTagId = await DbContextForArrange.Tags
            .Where(x => x.BoardId == secondBoardId && x.NormalisedName == "BUG")
            .Select(x => x.Id)
            .SingleAsync();

        var updatedStylePropertiesJson = """{"leftColor":"#223344","rightColor":"#446688","textColorMode":"auto","borderMode":"auto"}""";
        var service = CreateService();

        // Act
        var result = await service.UpdateTagStyleAsync(firstBoardId, firstBoardTagId, new UpdateTagRequest(
            Name: "Bug",
            StyleName: "gradient",
            StylePropertiesJson: updatedStylePropertiesJson), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(firstBoardTagId, result.Data!.Id);
        Assert.Equal("gradient", result.Data.StyleName);
        Assert.Equal(updatedStylePropertiesJson, result.Data.StylePropertiesJson);

        var firstBoardStoredTag = await DbContextForAssert.Tags.SingleAsync(x => x.Id == firstBoardTagId);
        Assert.Equal(firstBoardId, firstBoardStoredTag.BoardId);
        Assert.Equal("gradient", firstBoardStoredTag.StyleName);
        Assert.Equal(updatedStylePropertiesJson, firstBoardStoredTag.StylePropertiesJson);

        var secondBoardStoredTag = await DbContextForAssert.Tags.SingleAsync(x => x.Id == secondBoardTagId);
        Assert.Equal(secondBoardId, secondBoardStoredTag.BoardId);
        Assert.Equal("solid", secondBoardStoredTag.StyleName);
        Assert.Equal("""{"backgroundColor":"#553311","textColorMode":"auto"}""", secondBoardStoredTag.StylePropertiesJson);
    }

    [Fact]
    public async Task UpdateTagStyleAsync_WhenTagMissing_ShouldReturnNotFound()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        var stylePropertiesJson = """{"backgroundColor":"#224466","textColorMode":"auto"}""";

        // Act
        var service = CreateService();
        var result = await service.UpdateTagStyleAsync(boardId, 999_999, new UpdateTagRequest(
            Name: "Bug",
            StyleName: "solid",
            StylePropertiesJson: stylePropertiesJson), ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Tag not found.", result.Message);
        Assert.Empty(await DbContextForAssert.Tags.ToListAsync());
    }

    [Fact]
    public async Task UpdateTagDefinitionAsync_WhenNameOnly_ShouldPreserveEmojiAndStyleExactly()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        const string originalStyle = """{"backgroundColor":"#114488","textColorMode":"auto","borderMode":"auto","futureValue":true}""";
        DbContextForArrange.Tags.Add(new TagEntity
        {
            BoardId = boardId,
            Name = "Bug",
            NormalisedName = "BUG",
            StyleName = "solid",
            StylePropertiesJson = originalStyle,
            Emoji = "🐞"
        });
        await DbContextForArrange.SaveChangesAsync();
        var service = CreateService();

        // Act
        var result = await service.UpdateTagDefinitionAsync(
            boardId,
            "bug",
            new TagDefinitionPatch(true, "Platform", false, null, null),
            ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Platform", result.Data!.Name);
        Assert.Equal("🐞", result.Data.Emoji);
        Assert.Equal(originalStyle, result.Data.StylePropertiesJson);

        var stored = await DbContextForAssert.Tags.SingleAsync();
        Assert.Equal("Platform", stored.Name);
        Assert.Equal("🐞", stored.Emoji);
        Assert.Equal(originalStyle, stored.StylePropertiesJson);
    }

    [Fact]
    public async Task UpdateTagDefinitionAsync_WhenStyleOnly_ShouldPreserveNameAndEmoji()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        DbContextForArrange.Tags.Add(new TagEntity
        {
            BoardId = boardId,
            Name = "Bug",
            NormalisedName = "BUG",
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#114488","textColorMode":"auto","borderMode":"auto"}""",
            Emoji = "🐞"
        });
        await DbContextForArrange.SaveChangesAsync();
        var service = CreateService();
        var style = new TagStylePatch(
            "gradient",
            """{"leftColor":"#112233","rightColor":"#445566","textColorMode":"auto","borderMode":"none"}""");

        // Act
        var result = await service.UpdateTagDefinitionAsync(
            boardId,
            "Bug",
            new TagDefinitionPatch(false, null, false, null, style),
            ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Bug", result.Data!.Name);
        Assert.Equal("🐞", result.Data.Emoji);
        Assert.Equal("gradient", result.Data.StyleName);
    }

    [Fact]
    public async Task UpdateTagDefinitionAsync_WhenExistingStyleCannotBeRepresented_ShouldRequireReplacement()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        DbContextForArrange.Tags.Add(new TagEntity
        {
            BoardId = boardId,
            Name = "Bug",
            NormalisedName = "BUG",
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"blue"}""",
            Emoji = "🐞"
        });
        await DbContextForArrange.SaveChangesAsync();
        var service = CreateService();

        // Act
        var result = await service.UpdateTagDefinitionAsync(
            boardId,
            "Bug",
            new TagDefinitionPatch(false, null, true, "⚠️", null),
            ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("style"));

        var stored = await DbContextForAssert.Tags.SingleAsync();
        Assert.Equal("🐞", stored.Emoji);
    }

    [Fact]
    public async Task DeleteTagAsync_WhenTagExists_ShouldRemoveTagAndCardTagLinksOnly()
    {
        // Arrange
        var board = CreateBoard()
            .AddColumn("Todo")
            .AddCard("Task A")
            .Build();
        var boardId = board.BoardId;
        var cardId = board.GetCard("Todo", "Task A").Id;

        var now = DateTime.UtcNow;
        DbContextForArrange.Tags.Add(new TagEntity
        {
            BoardId = boardId,
            Name = "Bug",
            NormalisedName = "BUG",
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#114488","textColorMode":"auto"}""",
        });
        await DbContextForArrange.SaveChangesAsync();
        var tagId = await DbContextForArrange.Tags.Select(x => x.Id).SingleAsync();

        DbContextForArrange.CardTags.Add(new CardTagEntity
        {
            CardId = cardId,
            TagId = tagId
        });
        await DbContextForArrange.SaveChangesAsync();

        var service = CreateService();

        // Act
        var result = await service.DeleteTagAsync(boardId, tagId, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Empty(await DbContextForAssert.Tags.ToListAsync());
        Assert.Empty(await DbContextForAssert.CardTags.ToListAsync());
        Assert.Single(await DbContextForAssert.Cards.ToListAsync());
        Assert.Equal([boardId], ResolveBoardEvents().ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task DeleteTagAsync_WhenTagMissing_ShouldReturnOk()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        var service = CreateService();

        // Act
        var result = await service.DeleteTagAsync(boardId, 999_999, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Empty(ResolveBoardEvents().ResyncRequestedBoardIds);
    }

    private TagService CreateService()
    {
        return ResolveService<TagService>();
    }

    private void SeedPresetTags(int boardId, params int[] presetIndexes)
    {
        DbContextForArrange.Tags.AddRange(presetIndexes.Select(presetIndex => new TagEntity
        {
            BoardId = boardId,
            Name = $"Tag {presetIndex}",
            NormalisedName = $"TAG {presetIndex}",
            StyleName = "presets",
            StylePropertiesJson = $$"""{"presetIndex":{{presetIndex}},"textColorMode":"auto"}""",
        }));
    }

    private static int ReadPresetIndex(string stylePropertiesJson)
    {
        using var document = JsonDocument.Parse(stylePropertiesJson);
        return document.RootElement.GetProperty("presetIndex").GetInt32();
    }

    private TestBoardEvents ResolveBoardEvents() =>
        Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
}
