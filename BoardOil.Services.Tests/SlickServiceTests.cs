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
        var result = await service.CreateSlickAsync(boardId, new CreateSlickRequest("Release train", "auto", "{}"), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("Release train", result.Data!.Name);
        Assert.Equal("auto", result.Data.StyleName);
        var stored = await DbContextForAssert.Slicks.SingleAsync();
        Assert.Equal("RELEASE TRAIN", stored.NormalisedName);
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
            StyleName = "auto",
            StylePropertiesJson = "{}"
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
            StyleName = "auto",
            StylePropertiesJson = "{}"
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
    }

    private ISlickService CreateService() => ResolveService<ISlickService>();
}
