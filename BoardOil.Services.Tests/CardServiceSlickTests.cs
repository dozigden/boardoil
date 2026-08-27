using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Card;
using BoardOil.Services.Style;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SlickEntity = BoardOil.Data.Abstractions.Entities.EntitySlick;

namespace BoardOil.Services.Tests;

public sealed class CardServiceSlickTests : TestBaseDb
{
    [Fact]
    public async Task CreateCardAsync_WhenSlickNameMatchesExistingInBoard_ShouldPersistSlickMembership()
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
            new CreateCardRequest(todoColumnId, "Card A", "Desc", [], null, null, slick.Name),
            ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(slick.Id, result.Data!.SlickId);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(slick.Id, stored.SlickId);
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
            new UpdateCardRequest("Card A", "Desc", [], systemCardTypeId, null, null, slick.Name),
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

    [Fact]
    public async Task CreateCardAsync_WhenSlickNameProvided_ShouldAutoCreateAndAssignSlick()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var todoColumnId = board.GetColumn("Todo").Id;
        var service = CreateService();

        // Act
        var result = await service.CreateCardAsync(
            board.BoardId,
            new CreateCardRequest(todoColumnId, "Card A", "Desc", [], null, null, "Release train"),
            ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.NotNull(result.Data!.SlickId);

        var createdSlick = await DbContextForAssert.Slicks.SingleAsync(x => x.BoardId == board.BoardId);
        Assert.Equal("Release train", createdSlick.Name);
        Assert.Equal("RELEASE TRAIN", createdSlick.NormalisedName);
        Assert.Equal(StyleDefinitionCodec.PresetsStyleName, createdSlick.StyleName);

        var storedCard = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(createdSlick.Id, storedCard.SlickId);
    }

    [Fact]
    public async Task UpdateCardAsync_WhenSlickNameProvided_ShouldAutoCreateAndAssignSlick()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Card A", "Desc")
            .Build();
        var cardId = board.GetCard("Card A").Id;
        var systemCardTypeId = await DbContextForArrange.CardTypes.Where(x => x.BoardId == board.BoardId && x.IsSystem).Select(x => x.Id).SingleAsync();
        var service = CreateService();

        // Act
        var result = await service.UpdateCardAsync(
            board.BoardId,
            cardId,
            new UpdateCardRequest("Card A", "Desc", [], systemCardTypeId, null, null, "Release candidate"),
            ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.NotNull(result.Data!.SlickId);

        var createdSlick = await DbContextForAssert.Slicks.SingleAsync(x => x.BoardId == board.BoardId);
        Assert.Equal("Release candidate", createdSlick.Name);
        Assert.Equal("RELEASE CANDIDATE", createdSlick.NormalisedName);
        Assert.Equal(StyleDefinitionCodec.PresetsStyleName, createdSlick.StyleName);

        var storedCard = await DbContextForAssert.Cards.SingleAsync(x => x.Id == cardId);
        Assert.Equal(createdSlick.Id, storedCard.SlickId);
    }

    [Fact]
    public async Task BulkEditCardsAsync_WhenSlickNameProvided_ShouldAutoCreateAndAssignSlick()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Card A", "Desc")
            .AddCard("Card B", "Desc")
            .Build();
        var cardAId = board.GetCard("Todo", "Card A").Id;
        var cardBId = board.GetCard("Todo", "Card B").Id;
        var service = CreateService();

        // Act
        var result = await service.BulkEditCardsAsync(
            board.BoardId,
            new BulkEditCardsRequest([cardAId, cardBId], Move: null, Slick: new BulkEditSlickRequest("Release candidate")),
            ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.All(result.Data!, card => Assert.NotNull(card.SlickId));

        var createdSlick = await DbContextForAssert.Slicks.SingleAsync(x => x.BoardId == board.BoardId);
        Assert.Equal("Release candidate", createdSlick.Name);
        Assert.Equal("RELEASE CANDIDATE", createdSlick.NormalisedName);
        Assert.Equal(StyleDefinitionCodec.PresetsStyleName, createdSlick.StyleName);

        var storedCards = await DbContextForAssert.Cards.Where(x => x.Id == cardAId || x.Id == cardBId).ToListAsync();
        Assert.All(storedCards, card => Assert.Equal(createdSlick.Id, card.SlickId));
    }

    [Fact]
    public async Task BulkEditCardsAsync_WhenClearingSlick_ShouldRemoveMembership()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Card A", "Desc")
            .AddCard("Card B", "Desc")
            .Build();
        var cardAId = board.GetCard("Todo", "Card A").Id;
        var cardBId = board.GetCard("Todo", "Card B").Id;
        var slick = new SlickEntity
        {
            BoardId = board.BoardId,
            Name = "Release train",
            NormalisedName = "RELEASE TRAIN",
            StyleName = StyleDefinitionCodec.PresetsStyleName,
            StylePropertiesJson = StyleDefinitionCodec.Serialise(
                StyleDefinitionCodec.CreateDefault(StyleKind.Presets))
        };
        DbContextForArrange.Slicks.Add(slick);
        await DbContextForArrange.SaveChangesAsync();
        var cardsForArrange = await DbContextForArrange.Cards.Where(x => x.Id == cardAId || x.Id == cardBId).ToListAsync();
        foreach (var card in cardsForArrange)
        {
            card.SlickId = slick.Id;
        }

        await DbContextForArrange.SaveChangesAsync();
        var service = CreateService();

        // Act
        var result = await service.BulkEditCardsAsync(
            board.BoardId,
            new BulkEditCardsRequest([cardAId, cardBId], Move: null, Slick: new BulkEditSlickRequest(null)),
            ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.All(result.Data!, card => Assert.Null(card.SlickId));
        var storedCards = await DbContextForAssert.Cards.Where(x => x.Id == cardAId || x.Id == cardBId).ToListAsync();
        Assert.All(storedCards, card => Assert.Null(card.SlickId));
    }

    [Fact]
    public async Task BulkEditCardsAsync_WhenSlickNameTooLong_ShouldReturnValidationError()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Card A", "Desc")
            .Build();
        var cardId = board.GetCard("Todo", "Card A").Id;
        var service = CreateService();
        var overlongSlickName = new string('X', 41);

        // Act
        var result = await service.BulkEditCardsAsync(
            board.BoardId,
            new BulkEditCardsRequest([cardId], Move: null, Slick: new BulkEditSlickRequest(overlongSlickName)),
            ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("slick.name"));
    }

    private ICardService CreateService() => ResolveService<ICardService>();
}
