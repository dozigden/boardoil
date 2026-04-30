using BoardOil.Services.Card;
using BoardOil.Services.Tests.Infrastructure;
using BoardOil.Abstractions.Card;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardCommentServiceTests : TestBaseDb
{
    [Fact]
    public async Task CreateCommentAsync_WhenValid_ShouldPersistAndReturnCreated()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Task A", "Desc")
            .Build();
        var cardId = board.GetCard("Todo", "Task A").Id;
        var service = ResolveService<ICardCommentService>();

        // Act
        var result = await service.CreateCommentAsync(board.BoardId, cardId, new("  First comment  "), ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(cardId, result.Data!.CardId);
        Assert.Equal("First comment", result.Data.Text);

        var stored = await DbContextForAssert.CardComments.SingleAsync();
        Assert.Equal(cardId, stored.CardId);
        Assert.Equal(ActorUserId, stored.AuthorUserId);
        Assert.Equal("First comment", stored.Text);
    }

    [Fact]
    public async Task GetCommentsAsync_WhenCommentsExist_ShouldReturnCreatedOrder()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Task A", "Desc")
            .Build();
        var cardId = board.GetCard("Todo", "Task A").Id;
        var now = DateTime.UtcNow;
        DbContextForArrange.CardComments.Add(new()
        {
            CardId = cardId,
            AuthorUserId = ActorUserId,
            Text = "A",
            CreatedAtUtc = now
        });
        DbContextForArrange.CardComments.Add(new()
        {
            CardId = cardId,
            AuthorUserId = ActorUserId,
            Text = "B",
            CreatedAtUtc = now.AddSeconds(1)
        });
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardCommentService>();

        // Act
        var result = await service.GetCommentsAsync(board.BoardId, cardId, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(["B", "A"], result.Data!.Select(x => x.Text).ToArray());
    }
}
