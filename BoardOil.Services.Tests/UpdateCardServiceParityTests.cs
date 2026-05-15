using BoardOil.Contracts.Card;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Services.Card;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class UpdateCardServiceParityTests : TestBaseDb
{
    [Fact]
    public async Task ExecuteAsync_WhenUpdatingTitleOnly_ShouldMatchLegacyContract()
    {
        // Arrange
        var legacyBoard = CreateBoard("Legacy")
            .AddColumn("Todo")
            .AddCard("Old", "Desc")
            .Build();
        var parallelBoard = CreateBoard("Parallel")
            .AddColumn("Todo")
            .AddCard("Old", "Desc")
            .Build();

        var legacyCardId = legacyBoard.GetCard("Todo", "Old").Id;
        var parallelCardId = parallelBoard.GetCard("Todo", "Old").Id;
        var legacyCardTypeId = await GetSystemCardTypeIdForBoardAsync(legacyBoard.BoardId);
        var parallelCardTypeId = await GetSystemCardTypeIdForBoardAsync(parallelBoard.BoardId);

        var legacyService = ResolveService<CardService>();
        var updateCardService = ResolveService<UpdateCardService>();

        // Act
        var legacyResult = await legacyService.UpdateCardAsync(
            legacyBoard.BoardId,
            legacyCardId,
            new UpdateCardRequest("  New Title  ", "Desc", [], legacyCardTypeId),
            ActorUserId);

        var parallelResult = await updateCardService.ExecuteAsync(
            parallelBoard.BoardId,
            parallelCardId,
            new UpdateCardRequest("  New Title  ", "Desc", [], parallelCardTypeId),
            ActorUserId);

        // Assert
        Assert.Equal(legacyResult.Success, parallelResult.Success);
        Assert.Equal(legacyResult.StatusCode, parallelResult.StatusCode);
        Assert.NotNull(legacyResult.Data);
        Assert.NotNull(parallelResult.Data);
        Assert.Equal(legacyResult.Data!.Title, parallelResult.Data!.Title);
        Assert.Equal("New Title", parallelResult.Data.Title);
        Assert.Equal(legacyResult.Data.Description, parallelResult.Data.Description);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMovingColumns_ShouldMatchLegacyContract()
    {
        // Arrange
        var legacyBoard = CreateBoard("Legacy")
            .AddColumn("Todo")
            .AddCard("Move me", "source")
            .AddColumn("Doing")
            .AddCard("A", "1")
            .AddCard("B", "2")
            .Build();
        var parallelBoard = CreateBoard("Parallel")
            .AddColumn("Todo")
            .AddCard("Move me", "source")
            .AddColumn("Doing")
            .AddCard("A", "1")
            .AddCard("B", "2")
            .Build();

        var legacyCardId = legacyBoard.GetCard("Todo", "Move me").Id;
        var parallelCardId = parallelBoard.GetCard("Todo", "Move me").Id;
        var legacyDoingId = legacyBoard.GetColumn("Doing").Id;
        var parallelDoingId = parallelBoard.GetColumn("Doing").Id;
        var legacyCardTypeId = await GetSystemCardTypeIdForBoardAsync(legacyBoard.BoardId);
        var parallelCardTypeId = await GetSystemCardTypeIdForBoardAsync(parallelBoard.BoardId);

        var legacyService = ResolveService<CardService>();
        var updateCardService = ResolveService<UpdateCardService>();

        // Act
        var legacyResult = await legacyService.UpdateCardAsync(
            legacyBoard.BoardId,
            legacyCardId,
            new UpdateCardRequest("Move me updated", "updated", [], legacyCardTypeId, legacyDoingId),
            ActorUserId);

        var parallelResult = await updateCardService.ExecuteAsync(
            parallelBoard.BoardId,
            parallelCardId,
            new UpdateCardRequest("Move me updated", "updated", [], parallelCardTypeId, parallelDoingId),
            ActorUserId);

        // Assert
        Assert.Equal(legacyResult.Success, parallelResult.Success);
        Assert.Equal(legacyResult.StatusCode, parallelResult.StatusCode);
        Assert.NotNull(legacyResult.Data);
        Assert.NotNull(parallelResult.Data);
        Assert.Equal(legacyResult.Data!.BoardColumnId, legacyDoingId);
        Assert.Equal(parallelResult.Data!.BoardColumnId, parallelDoingId);

        var legacyDoingTitles = await GetOrderedTitlesAsync(DbContextForAssert, legacyDoingId);
        var parallelDoingTitles = await GetOrderedTitlesAsync(DbContextForAssert, parallelDoingId);
        Assert.Equal(["Move me updated", "A", "B"], legacyDoingTitles);
        Assert.Equal(["Move me updated", "A", "B"], parallelDoingTitles);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCardMissing_ShouldMatchLegacyNotFoundContract()
    {
        // Arrange
        var legacyBoard = CreateBoard("Legacy")
            .AddColumn("Todo")
            .Build();
        var parallelBoard = CreateBoard("Parallel")
            .AddColumn("Todo")
            .Build();

        var legacyCardTypeId = await GetSystemCardTypeIdForBoardAsync(legacyBoard.BoardId);
        var parallelCardTypeId = await GetSystemCardTypeIdForBoardAsync(parallelBoard.BoardId);

        var legacyService = ResolveService<CardService>();
        var updateCardService = ResolveService<UpdateCardService>();

        // Act
        var legacyResult = await legacyService.UpdateCardAsync(
            legacyBoard.BoardId,
            999_999,
            new UpdateCardRequest("X", string.Empty, [], legacyCardTypeId),
            ActorUserId);

        var parallelResult = await updateCardService.ExecuteAsync(
            parallelBoard.BoardId,
            999_999,
            new UpdateCardRequest("X", string.Empty, [], parallelCardTypeId),
            ActorUserId);

        // Assert
        Assert.Equal(legacyResult.Success, parallelResult.Success);
        Assert.Equal(legacyResult.StatusCode, parallelResult.StatusCode);
        Assert.Equal(legacyResult.Message, parallelResult.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTargetColumnMissing_ShouldMatchLegacyValidationContract()
    {
        // Arrange
        var legacyBoard = CreateBoard("Legacy")
            .AddColumn("Todo")
            .AddCard("Card", "Desc")
            .AddColumn("Doing")
            .Build();
        var parallelBoard = CreateBoard("Parallel")
            .AddColumn("Todo")
            .AddCard("Card", "Desc")
            .AddColumn("Doing")
            .Build();

        var legacyCardId = legacyBoard.GetCard("Todo", "Card").Id;
        var parallelCardId = parallelBoard.GetCard("Todo", "Card").Id;
        var legacyCardTypeId = await GetSystemCardTypeIdForBoardAsync(legacyBoard.BoardId);
        var parallelCardTypeId = await GetSystemCardTypeIdForBoardAsync(parallelBoard.BoardId);

        var legacyService = ResolveService<CardService>();
        var updateCardService = ResolveService<UpdateCardService>();

        // Act
        var legacyResult = await legacyService.UpdateCardAsync(
            legacyBoard.BoardId,
            legacyCardId,
            new UpdateCardRequest("Card", "Desc", [], legacyCardTypeId, 999_999),
            ActorUserId);

        var parallelResult = await updateCardService.ExecuteAsync(
            parallelBoard.BoardId,
            parallelCardId,
            new UpdateCardRequest("Card", "Desc", [], parallelCardTypeId, 999_999),
            ActorUserId);

        // Assert
        Assert.Equal(legacyResult.Success, parallelResult.Success);
        Assert.Equal(legacyResult.StatusCode, parallelResult.StatusCode);
        Assert.NotNull(legacyResult.ValidationErrors);
        Assert.NotNull(parallelResult.ValidationErrors);
        Assert.True(legacyResult.ValidationErrors!.ContainsKey("boardColumnId"));
        Assert.True(parallelResult.ValidationErrors!.ContainsKey("boardColumnId"));
    }

    private Task<int> GetSystemCardTypeIdForBoardAsync(int boardId) =>
        DbContextForArrange.CardTypes
            .Where(x => x.BoardId == boardId && x.IsSystem)
            .Select(x => x.Id)
            .SingleAsync();
}
