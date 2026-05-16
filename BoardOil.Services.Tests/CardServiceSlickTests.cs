using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Card;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SlickEntity = BoardOil.Data.Abstractions.Entities.EntitySlick;

namespace BoardOil.Services.Tests;

public sealed class CardServiceSlickTests : TestBaseDb
{
    [Fact]
    public async Task CreateCardAsync_WhenSlickInBoard_ShouldPersistSlickMembership()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var todoColumnId = board.GetColumn("Todo").Id;
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

        var service = CreateService();

        // Act
        var result = await service.CreateCardAsync(
            board.BoardId,
            new CreateCardRequest(todoColumnId, "Card A", "Desc", [], null, null, slick.Id),
            ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(slick.Id, result.Data!.SlickId);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(slick.Id, stored.SlickId);
    }

    [Fact]
    public async Task CreateCardAsync_WhenSlickFromDifferentBoard_ShouldReturnValidationError()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var otherBoard = CreateBoard("Other")
            .AddColumn("Todo")
            .Build();
        var todoColumnId = board.GetColumn("Todo").Id;
        var foreignSlick = new SlickEntity
        {
            BoardId = otherBoard.BoardId,
            Name = "Foreign",
            NormalisedName = "FOREIGN",
            StyleName = "auto",
            StylePropertiesJson = "{}"
        };
        DbContextForArrange.Slicks.Add(foreignSlick);
        await DbContextForArrange.SaveChangesAsync();

        var service = CreateService();

        // Act
        var result = await service.CreateCardAsync(
            board.BoardId,
            new CreateCardRequest(todoColumnId, "Card A", "Desc", [], null, null, foreignSlick.Id),
            ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("slickId"));
    }

    [Fact]
    public async Task UpdateCardAsync_WhenSlickChanged_ShouldPersistUpdatedMembership()
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
        var systemCardTypeId = await DbContextForArrange.CardTypes.Where(x => x.BoardId == board.BoardId && x.IsSystem).Select(x => x.Id).SingleAsync();
        var service = CreateService();

        // Act
        var setResult = await service.UpdateCardAsync(
            board.BoardId,
            cardId,
            new UpdateCardRequest("Card A", "Desc", [], systemCardTypeId, null, null, slick.Id),
            ActorUserId);
        var clearResult = await service.UpdateCardAsync(
            board.BoardId,
            cardId,
            new UpdateCardRequest("Card A", "Desc", [], systemCardTypeId, null, null, null),
            ActorUserId);

        // Assert
        Assert.True(setResult.Success);
        Assert.True(clearResult.Success);
        Assert.Equal(slick.Id, setResult.Data!.SlickId);
        Assert.Null(clearResult.Data!.SlickId);
        var stored = await DbContextForAssert.Cards.SingleAsync(x => x.Id == cardId);
        Assert.Null(stored.SlickId);
    }

    private ICardService CreateService() => ResolveService<ICardService>();
}
