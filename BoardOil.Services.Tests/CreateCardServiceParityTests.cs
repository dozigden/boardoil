using BoardOil.Contracts.Card;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Services.Card;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using TagEntity = BoardOil.Persistence.Abstractions.Entities.EntityTag;

namespace BoardOil.Services.Tests;

public sealed class CreateCardServiceParityTests : TestBaseDb
{
    [Fact]
    public async Task ExecuteAsync_WhenRequestIsValid_ShouldMatchLegacyCreateContract()
    {
        // Arrange
        var legacyBoard = CreateBoard("Legacy")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();
        var parallelBoard = CreateBoard("Parallel")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();
        await SeedTagsForArrangeAsync(legacyBoard.BoardId, "Bug", "Ops");
        await SeedTagsForArrangeAsync(parallelBoard.BoardId, "Bug", "Ops");
        var legacyTodoColumnId = legacyBoard.GetColumn("Todo").Id;
        var parallelTodoColumnId = parallelBoard.GetColumn("Todo").Id;

        var legacyService = ResolveService<CardService>();
        var parallelUseCase = ResolveService<CreateCardService>();

        // Act
        var legacyResult = await legacyService.CreateCardAsync(
            legacyBoard.BoardId,
            new CreateCardRequest(legacyTodoColumnId, "  New Card  ", "Desc", ["Bug", "Ops"], null),
            ActorUserId);

        var parallelResult = await parallelUseCase.ExecuteAsync(
            parallelBoard.BoardId,
            new CreateCardRequest(parallelTodoColumnId, "  New Card  ", "Desc", ["Bug", "Ops"], null),
            ActorUserId);

        // Assert
        Assert.Equal(legacyResult.Success, parallelResult.Success);
        Assert.Equal(legacyResult.StatusCode, parallelResult.StatusCode);
        Assert.NotNull(legacyResult.Data);
        Assert.NotNull(parallelResult.Data);

        Assert.Equal("New Card", legacyResult.Data!.Title);
        Assert.Equal("New Card", parallelResult.Data!.Title);
        Assert.Equal(legacyResult.Data.Description, parallelResult.Data.Description);
        Assert.Equal(legacyResult.Data.TagNames, parallelResult.Data.TagNames);
        Assert.Equal(legacyResult.Data.CardTypeName, parallelResult.Data.CardTypeName);
        Assert.Equal(legacyTodoColumnId, legacyResult.Data.BoardColumnId);
        Assert.Equal(parallelTodoColumnId, parallelResult.Data.BoardColumnId);
        Assert.False(string.IsNullOrWhiteSpace(legacyResult.Data.SortKey));
        Assert.False(string.IsNullOrWhiteSpace(parallelResult.Data.SortKey));
    }

    [Fact]
    public async Task ExecuteAsync_WhenBoardColumnIdOmitted_ShouldMatchLegacyDefaultColumnSelection()
    {
        // Arrange
        var legacyBoard = CreateBoard("Legacy")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();
        var parallelBoard = CreateBoard("Parallel")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();
        var legacyLeftMostColumnId = legacyBoard.GetColumn("Todo").Id;
        var parallelLeftMostColumnId = parallelBoard.GetColumn("Todo").Id;

        var legacyService = ResolveService<CardService>();
        var parallelUseCase = ResolveService<CreateCardService>();

        // Act
        var legacyResult = await legacyService.CreateCardAsync(
            legacyBoard.BoardId,
            new CreateCardRequest(null, "New Card", "Desc", null),
            ActorUserId);

        var parallelResult = await parallelUseCase.ExecuteAsync(
            parallelBoard.BoardId,
            new CreateCardRequest(null, "New Card", "Desc", null),
            ActorUserId);

        // Assert
        Assert.Equal(legacyResult.Success, parallelResult.Success);
        Assert.Equal(legacyResult.StatusCode, parallelResult.StatusCode);
        Assert.NotNull(legacyResult.Data);
        Assert.NotNull(parallelResult.Data);
        Assert.Equal(legacyLeftMostColumnId, legacyResult.Data!.BoardColumnId);
        Assert.Equal(parallelLeftMostColumnId, parallelResult.Data!.BoardColumnId);
        Assert.Equal(legacyResult.Data.Description, parallelResult.Data.Description);
        Assert.Equal(legacyResult.Data.CardTypeName, parallelResult.Data.CardTypeName);
    }

    [Fact]
    public async Task ExecuteAsync_WhenColumnMissing_ShouldMatchLegacyValidationContract()
    {
        // Arrange
        var legacyBoard = CreateBoard("Legacy")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();
        var parallelBoard = CreateBoard("Parallel")
            .AddColumn("Todo")
            .AddColumn("Doing")
            .Build();

        var legacyService = ResolveService<CardService>();
        var parallelUseCase = ResolveService<CreateCardService>();

        // Act
        var legacyResult = await legacyService.CreateCardAsync(
            legacyBoard.BoardId,
            new CreateCardRequest(999_999, "New", "Desc", null),
            ActorUserId);

        var parallelResult = await parallelUseCase.ExecuteAsync(
            parallelBoard.BoardId,
            new CreateCardRequest(999_999, "New", "Desc", null),
            ActorUserId);

        // Assert
        Assert.Equal(legacyResult.Success, parallelResult.Success);
        Assert.Equal(legacyResult.StatusCode, parallelResult.StatusCode);
        Assert.NotNull(legacyResult.ValidationErrors);
        Assert.NotNull(parallelResult.ValidationErrors);
        Assert.True(legacyResult.ValidationErrors!.ContainsKey("boardColumnId"));
        Assert.True(parallelResult.ValidationErrors!.ContainsKey("boardColumnId"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCardTypeMissing_ShouldMatchLegacyValidationContract()
    {
        // Arrange
        var legacyBoard = CreateBoard("Legacy")
            .AddColumn("Todo")
            .Build();
        var parallelBoard = CreateBoard("Parallel")
            .AddColumn("Todo")
            .Build();
        var legacyTodoColumnId = legacyBoard.GetColumn("Todo").Id;
        var parallelTodoColumnId = parallelBoard.GetColumn("Todo").Id;

        var legacyService = ResolveService<CardService>();
        var parallelUseCase = ResolveService<CreateCardService>();

        // Act
        var legacyResult = await legacyService.CreateCardAsync(
            legacyBoard.BoardId,
            new CreateCardRequest(legacyTodoColumnId, "New", "Desc", null, 999_999),
            ActorUserId);

        var parallelResult = await parallelUseCase.ExecuteAsync(
            parallelBoard.BoardId,
            new CreateCardRequest(parallelTodoColumnId, "New", "Desc", null, 999_999),
            ActorUserId);

        // Assert
        Assert.Equal(legacyResult.Success, parallelResult.Success);
        Assert.Equal(legacyResult.StatusCode, parallelResult.StatusCode);
        Assert.NotNull(legacyResult.ValidationErrors);
        Assert.NotNull(parallelResult.ValidationErrors);
        Assert.True(legacyResult.ValidationErrors!.ContainsKey("cardTypeId"));
        Assert.True(parallelResult.ValidationErrors!.ContainsKey("cardTypeId"));
    }

    private async Task SeedTagsForArrangeAsync(int boardId, params string[] tagNames)
    {
        DbContextForArrange.Tags.AddRange(tagNames.Select(tagName => new TagEntity
        {
            BoardId = boardId,
            Name = tagName,
            NormalisedName = tagName.ToUpperInvariant(),
            StyleName = "solid",
            StylePropertiesJson = """{"backgroundColor":"#224466","textColorMode":"auto"}""",
        }));
        await DbContextForArrange.SaveChangesAsync();
    }
}
