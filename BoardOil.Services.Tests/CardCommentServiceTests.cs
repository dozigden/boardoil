using BoardOil.Services.Card;
using BoardOil.Services.Tests.Infrastructure;
using BoardOil.Abstractions;
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
        var boardEvents = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());

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

        var realtimeEvent = Assert.Single(boardEvents.CommentCreatedEvents);
        Assert.Equal(board.BoardId, realtimeEvent.BoardId);
        Assert.Equal(cardId, realtimeEvent.Comment.CardId);
        Assert.Equal("First comment", realtimeEvent.Comment.Text);
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

    [Fact]
    public async Task GetCommentsAsync_WhenAuthorCannotBeResolved_ShouldReturnUnknownUser()
    {
        // Arrange
        var board = CreateBoard("BoardOil")
            .AddColumn("Todo")
            .AddCard("Task A", "Desc")
            .Build();
        var cardId = board.GetCard("Todo", "Task A").Id;
        DbContextForArrange.CardComments.Add(new()
        {
            CardId = cardId,
            AuthorUserId = null,
            Text = "Orphaned",
            CreatedAtUtc = DateTime.UtcNow
        });
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardCommentService>();

        // Act
        var result = await service.GetCommentsAsync(board.BoardId, cardId, ActorUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var comment = Assert.Single(result.Data!);
        Assert.Null(comment.AuthorUserId);
        Assert.Equal("Unknown user", comment.AuthorDisplayName);
        Assert.Null(comment.AuthorImageRelativePath);
    }
}
