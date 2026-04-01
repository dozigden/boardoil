using BoardOil.Contracts.Tag;
using BoardOil.Services.Tag;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;
using CardTagEntity = BoardOil.Persistence.Abstractions.Entities.EntityCardTag;
using TagEntity = BoardOil.Persistence.Abstractions.Entities.EntityTag;

namespace BoardOil.Services.Tests;

public sealed class TagServiceTests : TestBaseDb
{
    private static readonly HashSet<string> DefaultTagPalette = new(StringComparer.OrdinalIgnoreCase)
    {
        "#35165A",
        "#9D8ABF",
        "#69C1CE",
        "#E8C07D",
        "#CD474E",
        "#9BBEF8",
        "#F17437",
        "#32CDA0"
    };

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
        var result = await service.CreateTagAsync(boardId, new CreateTagRequest("Bug"));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("Bug", result.Data!.Name);
        Assert.Equal("solid", result.Data.StyleName);

        var stored = await DbContextForAssert.Tags.SingleAsync();
        Assert.Equal(boardId, stored.BoardId);
        Assert.Equal("Bug", stored.Name);
        Assert.Equal("BUG", stored.NormalisedName);
        Assert.Equal("solid", stored.StyleName);
        Assert.NotEmpty(stored.StylePropertiesJson);

        using var styleProperties = JsonDocument.Parse(stored.StylePropertiesJson);
        Assert.True(styleProperties.RootElement.TryGetProperty("backgroundColor", out var backgroundColor));
        Assert.Contains(backgroundColor.GetString() ?? string.Empty, DefaultTagPalette);
        Assert.True(styleProperties.RootElement.TryGetProperty("textColorMode", out var textColorMode));
        Assert.Equal("auto", textColorMode.GetString());
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
        var result = await service.CreateTagAsync(boardId, new CreateTagRequest("Bug", "🐞"));

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("🐞", result.Data!.Emoji);

        var stored = await DbContextForAssert.Tags.SingleAsync();
        Assert.Equal("🐞", stored.Emoji);
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
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await DbContextForArrange.SaveChangesAsync();

        var service = CreateService();

        // Act
        var result = await service.CreateTagAsync(boardId, new CreateTagRequest("Bug"));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("Bug", result.Data!.Name);
        Assert.Equal(1, await DbContextForAssert.Tags.CountAsync());
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
        var result = await service.CreateTagAsync(boardId, new CreateTagRequest("Bug,Urgent"));

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
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new TagEntity
            {
                BoardId = boardId,
                Name = "Urgent",
                NormalisedName = "URGENT",
                StyleName = "solid",
                StylePropertiesJson = """{"backgroundColor":"#AA3322","textColorMode":"auto"}""",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new TagEntity
            {
                BoardId = otherBoardId,
                Name = "Other",
                NormalisedName = "OTHER",
                StyleName = "solid",
                StylePropertiesJson = """{"backgroundColor":"#117733","textColorMode":"auto"}""",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        await DbContextForArrange.SaveChangesAsync();

        // Act
        var service = CreateService();
        var result = await service.GetTagsAsync(boardId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(["Bug", "Urgent"], result.Data!.Select(x => x.Name).ToArray());
    }

    [Fact]
    public async Task UpdateTagStyleAsync_WhenStyleInvalid_ShouldReturnValidationError()
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
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await DbContextForArrange.SaveChangesAsync();
        var tagId = await DbContextForArrange.Tags.Select(x => x.Id).SingleAsync();

        // Act
        var service = CreateService();
        var result = await service.UpdateTagStyleAsync(boardId, tagId, new UpdateTagStyleRequest(
            StyleName: "solid",
            StylePropertiesJson: """{"backgroundColor":"blue","textColorMode":"auto"}"""));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("stylePropertiesJson"));
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
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await DbContextForArrange.SaveChangesAsync();
        var tagId = await DbContextForArrange.Tags.Select(x => x.Id).SingleAsync();

        var updatedStylePropertiesJson = """{"leftColor":"#113355","rightColor":"#557799","textColorMode":"custom","textColor":"#FFFFFF"}""";

        // Act
        var service = CreateService();
        var result = await service.UpdateTagStyleAsync(boardId, tagId, new UpdateTagStyleRequest(
            StyleName: "gradient",
            StylePropertiesJson: updatedStylePropertiesJson));

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("gradient", result.Data!.StyleName);
        Assert.Equal(updatedStylePropertiesJson, result.Data.StylePropertiesJson);

        var stored = await DbContextForAssert.Tags.SingleAsync();
        Assert.Equal("gradient", stored.StyleName);
        Assert.Equal(updatedStylePropertiesJson, stored.StylePropertiesJson);
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
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await DbContextForArrange.SaveChangesAsync();
        var tagId = await DbContextForArrange.Tags.Select(x => x.Id).SingleAsync();

        // Act
        var service = CreateService();
        var result = await service.UpdateTagStyleAsync(boardId, tagId, new UpdateTagStyleRequest(
            StyleName: "solid",
            StylePropertiesJson: """{"backgroundColor":"#114488","textColorMode":"auto"}""",
            Emoji: "not-emoji"));

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
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await DbContextForArrange.SaveChangesAsync();
        var tagId = await DbContextForArrange.Tags.Select(x => x.Id).SingleAsync();
        var service = CreateService();

        // Act
        var setEmojiResult = await service.UpdateTagStyleAsync(boardId, tagId, new UpdateTagStyleRequest(
            StyleName: "solid",
            StylePropertiesJson: """{"backgroundColor":"#114488","textColorMode":"auto"}""",
            Emoji: "⚠️"));
        var clearEmojiResult = await service.UpdateTagStyleAsync(boardId, tagId, new UpdateTagStyleRequest(
            StyleName: "solid",
            StylePropertiesJson: """{"backgroundColor":"#114488","textColorMode":"auto"}""",
            Emoji: "   "));

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
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await DbContextForArrange.SaveChangesAsync();
        var tagId = await DbContextForArrange.Tags.Select(x => x.Id).SingleAsync();

        // Act
        var service = CreateService();
        var result = await service.UpdateTagStyleAsync(boardId, tagId, new UpdateTagStyleRequest(
            StyleName: "gradient",
            StylePropertiesJson: """{"leftColor":"#113355","rightColor":"#557799","textColorMode":"auto"}"""));

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
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new TagEntity
            {
                BoardId = secondBoardId,
                Name = "Bug",
                NormalisedName = "BUG",
                StyleName = "solid",
                StylePropertiesJson = """{"backgroundColor":"#553311","textColorMode":"auto"}""",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
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

        var updatedStylePropertiesJson = """{"leftColor":"#223344","rightColor":"#446688","textColorMode":"auto"}""";
        var service = CreateService();

        // Act
        var result = await service.UpdateTagStyleAsync(firstBoardId, firstBoardTagId, new UpdateTagStyleRequest(
            StyleName: "gradient",
            StylePropertiesJson: updatedStylePropertiesJson));

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
        var result = await service.UpdateTagStyleAsync(boardId, 999_999, new UpdateTagStyleRequest(
            StyleName: "solid",
            StylePropertiesJson: stylePropertiesJson));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Tag not found.", result.Message);
        Assert.Empty(await DbContextForAssert.Tags.ToListAsync());
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
            CreatedAtUtc = now,
            UpdatedAtUtc = now
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
        var result = await service.DeleteTagAsync(boardId, tagId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Empty(await DbContextForAssert.Tags.ToListAsync());
        Assert.Empty(await DbContextForAssert.CardTags.ToListAsync());
        Assert.Single(await DbContextForAssert.Cards.ToListAsync());
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
        var result = await service.DeleteTagAsync(boardId, 999_999);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
    }

    private TagService CreateService()
    {
        return ResolveService<TagService>();
    }
}
