using BoardOil.Abstractions;
using BoardOil.Abstractions.Slick;
using BoardOil.Contracts.Slick;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SlickEntity = BoardOil.Data.Abstractions.Entities.EntitySlick;

namespace BoardOil.Services.Tests;

public sealed class SlickServiceTests : TestBaseDb
{
    [Fact]
    public async Task CreateSlickAsync_WhenValid_ShouldCreateSlick()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        var service = CreateService();

        // Act
        var result = await service.CreateSlickAsync(
            boardId,
            new CreateSlickRequest("Release train", "presets", """{"presetIndex":2}"""),
            ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("Release train", result.Data!.Name);
        Assert.Equal("presets", result.Data.StyleName);
        var stored = await DbContextForAssert.Slicks.SingleAsync();
        Assert.Equal("RELEASE TRAIN", stored.NormalisedName);
        Assert.Equal([boardId], ResolveBoardEvents().ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task CreateSlickAsync_WhenStyleNameInvalid_ShouldReturnValidationError()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        var service = CreateService();

        // Act
        var result = await service.CreateSlickAsync(
            boardId,
            new CreateSlickRequest("Release train", "gradient", """{"leftColor":"#111111","rightColor":"#222222"}"""),
            ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("styleName"));
        Assert.Empty(ResolveBoardEvents().ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task UpdateSlickAsync_WhenStyleJsonIsNotObject_ShouldReturnValidationError()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        var slick = new SlickEntity
        {
            BoardId = boardId,
            Name = "Release train",
            NormalisedName = "RELEASE TRAIN",
            StyleName = "presets",
            StylePropertiesJson = """{"presetIndex":2}"""
        };
        DbContextForArrange.Slicks.Add(slick);
        await DbContextForArrange.SaveChangesAsync();
        var service = CreateService();

        // Act
        var result = await service.UpdateSlickAsync(
            boardId,
            slick.Id,
            new UpdateSlickRequest("Release train", "solid", """["bad"]"""),
            ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("stylePropertiesJson"));
        Assert.Empty(ResolveBoardEvents().ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task UpdateSlickAsync_WhenValid_ShouldPersistUpdateAndRequestResync()
    {
        // Arrange
        var boardId = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build()
            .BoardId;
        var slick = new SlickEntity
        {
            BoardId = boardId,
            Name = "Release train",
            NormalisedName = "RELEASE TRAIN",
            StyleName = "presets",
            StylePropertiesJson = """{"presetIndex":2}"""
        };
        DbContextForArrange.Slicks.Add(slick);
        await DbContextForArrange.SaveChangesAsync();
        var service = CreateService();

        // Act
        var result = await service.UpdateSlickAsync(
            boardId,
            slick.Id,
            new UpdateSlickRequest("Launch lane", "solid", """{"backgroundColor":"#224466"}"""),
            ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Launch lane", result.Data!.Name);
        Assert.Equal("solid", result.Data.StyleName);
        Assert.Equal([boardId], ResolveBoardEvents().ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task DeleteSlickAsync_WhenCardsAssigned_ShouldClearCardMembership()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Card A", "Desc")
            .Build();
        var cardId = board.GetCard("Card A").Id;
        var slick = new SlickEntity
        {
            BoardId = board.BoardId,
            Name = "Release train",
            NormalisedName = "RELEASE TRAIN",
            StyleName = "presets",
            StylePropertiesJson = """{"presetIndex":2}"""
        };
        DbContextForArrange.Slicks.Add(slick);
        await DbContextForArrange.SaveChangesAsync();
        var card = await DbContextForArrange.Cards.SingleAsync(x => x.Id == cardId);
        card.SlickId = slick.Id;
        await DbContextForArrange.SaveChangesAsync();

        var service = CreateService();

        // Act
        var result = await service.DeleteSlickAsync(board.BoardId, slick.Id, ActorUserId);

        // Assert
        Assert.True(result.Success);
        var storedCard = await DbContextForAssert.Cards.SingleAsync(x => x.Id == cardId);
        Assert.Null(storedCard.SlickId);
        Assert.Empty(await DbContextForAssert.Slicks.ToListAsync());
        Assert.Equal([board.BoardId], ResolveBoardEvents().ResyncRequestedBoardIds);
    }

    private ISlickService CreateService() => ResolveService<ISlickService>();

    private TestBoardEvents ResolveBoardEvents() =>
        Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
}
