using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Card;
using BoardOil.Services.Card;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardServiceAuthorisationTests : TestBaseDb
{
    private readonly CapturingBoardAuthorisationService _boardAuthorisationService = new();

    [Fact]
    public async Task ArchiveCardAsync_WhenPermissionDenied_ShouldCheckCardDeletePermission()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Archive me", "Desc")
            .Build();
        var cardId = board.GetCard("Todo", "Archive me").Id;
        var service = ResolveService<ICardArchiveService>();

        // Act
        var result = await service.ArchiveCardAsync(board.BoardId, cardId, ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(BoardPermission.CardDelete, _boardAuthorisationService.LastPermission);
        var cardStillExists = await DbContextForAssert.Cards.AnyAsync(x => x.Id == cardId);
        Assert.True(cardStillExists);
    }

    [Fact]
    public async Task ArchiveCardsAsync_WhenPermissionDenied_ShouldCheckCardDeletePermission()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Archive me", "Desc")
            .Build();
        var cardId = board.GetCard("Todo", "Archive me").Id;
        var service = ResolveService<ICardArchiveService>();

        // Act
        var result = await service.ArchiveCardsAsync(board.BoardId, new ArchiveCardsRequest([cardId]), ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(BoardPermission.CardDelete, _boardAuthorisationService.LastPermission);
        var cardStillExists = await DbContextForAssert.Cards.AnyAsync(x => x.Id == cardId);
        Assert.True(cardStillExists);
    }

    [Fact]
    public async Task GetArchivedCardsAsync_WhenPermissionDenied_ShouldCheckBoardAccessPermission()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var service = ResolveService<ICardArchiveService>();

        // Act
        var result = await service.GetArchivedCardsAsync(board.BoardId, search: null, offset: null, limit: null, ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(BoardPermission.BoardAccess, _boardAuthorisationService.LastPermission);
    }

    [Fact]
    public async Task GetArchivedCardAsync_WhenPermissionDenied_ShouldCheckBoardAccessPermission()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .Build();
        var service = ResolveService<ICardArchiveService>();

        // Act
        var result = await service.GetArchivedCardAsync(board.BoardId, archivedCardId: 123, ActorUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(BoardPermission.BoardAccess, _boardAuthorisationService.LastPermission);
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton(_boardAuthorisationService);
        services.AddScoped<IBoardAuthorisationService>(provider =>
            provider.GetRequiredService<CapturingBoardAuthorisationService>());
    }

    private sealed class CapturingBoardAuthorisationService : IBoardAuthorisationService
    {
        public BoardPermission? LastPermission { get; private set; }

        public Task<bool> HasPermissionAsync(int boardId, int actorUserId, BoardPermission permission)
        {
            LastPermission = permission;
            return Task.FromResult(false);
        }
    }
}
