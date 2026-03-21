using BoardOil.Abstractions.Tag;
using BoardOil.Contracts.Tag;
using BoardOil.Ef.Repositories;
using BoardOil.Services.Tag;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using TagEntity = BoardOil.Ef.Entities.EntityTag;

namespace BoardOil.Services.Tests;

public sealed class TagServiceTests : TestBaseDb
{
    [Fact]
    public async Task CreateTagAsync_WhenTagMissing_ShouldCreateTagWithDefaultStyle()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.CreateTagAsync(new CreateTagRequest("Bug"));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("Bug", result.Data!.Name);
        Assert.Equal("solid", result.Data.StyleName);

        var stored = await DbContextForAssert.Tags.SingleAsync();
        Assert.Equal("Bug", stored.Name);
        Assert.Equal("BUG", stored.NormalisedName);
        Assert.Equal("solid", stored.StyleName);
        Assert.NotEmpty(stored.StylePropertiesJson);
    }

    [Fact]
    public async Task CreateTagAsync_WhenTagAlreadyExists_ShouldReturnExistingTag()
    {
        // Arrange
        DbContextForArrange.Tags.Add(new TagEntity
        {
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
        var result = await service.CreateTagAsync(new CreateTagRequest("Bug"));

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
        var service = CreateService();

        // Act
        var result = await service.CreateTagAsync(new CreateTagRequest("Bug,Urgent"));

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
        DbContextForArrange.Tags.AddRange(
            new TagEntity
            {
                Name = "Bug",
                NormalisedName = "BUG",
                StyleName = "solid",
                StylePropertiesJson = """{"backgroundColor":"#114488","textColorMode":"auto"}""",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new TagEntity
            {
                Name = "Urgent",
                NormalisedName = "URGENT",
                StyleName = "solid",
                StylePropertiesJson = """{"backgroundColor":"#AA3322","textColorMode":"auto"}""",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        await DbContextForArrange.SaveChangesAsync();

        // Act
        var service = CreateService();
        var result = await service.GetTagsAsync();

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(["Bug", "Urgent"], result.Data!.Select(x => x.Name).ToArray());
    }

    [Fact]
    public async Task UpdateTagStyleAsync_WhenStyleInvalid_ShouldReturnValidationError()
    {
        // Arrange
        DbContextForArrange.Tags.Add(new TagEntity
        {
            Name = "Bug",
            NormalisedName = "BUG",
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#114488","textColorMode":"auto"}""",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await DbContextForArrange.SaveChangesAsync();

        // Act
        var service = CreateService();
        var result = await service.UpdateTagStyleAsync("Bug", new UpdateTagStyleRequest(
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
        DbContextForArrange.Tags.Add(new TagEntity
        {
            Name = "Bug",
            NormalisedName = "BUG",
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#114488","textColorMode":"auto"}""",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await DbContextForArrange.SaveChangesAsync();

        var updatedStylePropertiesJson = """{"leftColor":"#113355","rightColor":"#557799","textColorMode":"custom","textColor":"#FFFFFF"}""";

        // Act
        var service = CreateService();
        var result = await service.UpdateTagStyleAsync("Bug", new UpdateTagStyleRequest(
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
    public async Task UpdateTagStyleAsync_WhenTagMissing_ShouldReturnNotFound()
    {
        // Arrange
        var stylePropertiesJson = """{"backgroundColor":"#224466","textColorMode":"auto"}""";

        // Act
        var service = CreateService();
        var result = await service.UpdateTagStyleAsync("Bug", new UpdateTagStyleRequest(
            StyleName: "solid",
            StylePropertiesJson: stylePropertiesJson));

        // Assert
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Tag not found.", result.Message);
        Assert.Empty(await DbContextForAssert.Tags.ToListAsync());
    }

    private TagService CreateService()
    {
        return ResolveService<TagService>();
    }
}
