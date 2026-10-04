using BoardOil.Abstractions;
using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Card;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardCommentEditingServiceTests : TestBaseDb
{
    private static readonly DateTime PostedAtUtc = new(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(BoardMemberRole.Owner)]
    [InlineData(BoardMemberRole.Contributor)]
    public async Task UpdateCommentAsync_ShouldPreserveAttributionAndPostingTimeAndPublishChanges(BoardMemberRole role)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        DbContextForArrange.BoardMembers.Single(x => x.BoardId == board.BoardId).Role = role;
        var comment = await AddCommentAsync(card.Id, ActorUserId);
        var service = ResolveService<ICardCommentService>();

        var result = await service.UpdateCommentAsync(board.BoardId, card.BoardCardId, comment.Id, new("  Corrected **Markdown**  "), ActorUserId);

        Assert.True(result.Success, result.Message);
        var stored = await DbContextForAssert.CardComments.SingleAsync();
        Assert.Equal(comment.Id, stored.Id);
        Assert.Equal("Corrected **Markdown**", stored.Text);
        Assert.Equal(ActorUserId, stored.AuthorUserId);
        Assert.Equal(PostedAtUtc, stored.PostedAtUtc);
        Assert.Equal(PostedAtUtc, stored.CreatedAtUtc);
        Assert.NotNull(stored.EditedAtUtc);
        Assert.Equal(DateTimeKind.Utc, stored.EditedAtUtc.Value.Kind);
        Assert.Equal(stored.EditedAtUtc, result.Data!.EditedAtUtc);
        Assert.Equal(card.BoardCardId, result.Data.CardId);
        Assert.Equal(stored.EditedAtUtc, (await DbContextForAssert.Cards.SingleAsync()).CardUpdatedUtc);
        var events = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
        Assert.Equal(result.Data, Assert.Single(events.CommentUpdatedEvents).Comment);
        Assert.Equal(stored.EditedAtUtc, Assert.Single(events.CardUpdatedEvents).Card.CardUpdatedUtc);
        Assert.Empty(events.CommentCreatedEvents);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateCommentAsync_WhenTextIsUnchanged_ShouldPreserveAllTimestamps(bool previouslyEdited)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        var comment = await AddCommentAsync(card.Id, ActorUserId);
        if (previouslyEdited)
        {
            comment.EditedAtUtc = PostedAtUtc.AddHours(1);
            await DbContextForArrange.SaveChangesAsync();
        }

        var result = await ResolveService<ICardCommentService>().UpdateCommentAsync(
            board.BoardId, card.BoardCardId, comment.Id, new("  Original comment  "), ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(comment.EditedAtUtc, result.Data!.EditedAtUtc);
        var stored = await DbContextForAssert.Cards.SingleAsync();
        Assert.Equal(card.CardUpdatedUtc, stored.CardUpdatedUtc);
        Assert.Equal(card.UpdatedAtUtc, stored.UpdatedAtUtc);
        var events = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
        Assert.Empty(events.CommentUpdatedEvents);
        Assert.Empty(events.CardUpdatedEvents);
    }

    [Theory]
    [InlineData(BoardMemberRole.Owner, false)]
    [InlineData(BoardMemberRole.Contributor, false)]
    [InlineData(BoardMemberRole.Owner, true)]
    public async Task UpdateCommentAsync_WhenNotTheAuthor_ShouldForbidEvenBoardOwners(BoardMemberRole role, bool missingAuthor)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        DbContextForArrange.BoardMembers.Single(x => x.BoardId == board.BoardId).Role = role;
        int? authorUserId = null;
        if (!missingAuthor)
        {
            var author = new EntityUser { UserName = "author", Email = "author@example.com", NormalisedEmail = "author@example.com", PasswordHash = "test", IsActive = true };
            DbContextForArrange.Users.Add(author);
            await DbContextForArrange.SaveChangesAsync();
            authorUserId = author.Id;
        }
        var comment = await AddCommentAsync(card.Id, authorUserId);

        var result = await ResolveService<ICardCommentService>().UpdateCommentAsync(
            board.BoardId, card.BoardCardId, comment.Id, new("Replacement"), ActorUserId);

        Assert.Equal(403, result.StatusCode);
        Assert.Equal(comment.Text, (await DbContextForAssert.CardComments.SingleAsync()).Text);
        Assert.Empty(Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>()).CommentUpdatedEvents);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateCommentAsync_WhenMembershipRemovedOrAuthorInactive_ShouldForbid(bool inactive)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        var comment = await AddCommentAsync(card.Id, ActorUserId);
        if (inactive)
        {
            DbContextForArrange.Users.Single(x => x.Id == ActorUserId).IsActive = false;
        }
        else
        {
            DbContextForArrange.BoardMembers.Remove(DbContextForArrange.BoardMembers.Single(x => x.BoardId == board.BoardId));
        }
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<ICardCommentService>().UpdateCommentAsync(
            board.BoardId, card.BoardCardId, comment.Id, new("Replacement"), ActorUserId);

        Assert.Equal(403, result.StatusCode);
        Assert.Equal(comment.Text, (await DbContextForAssert.CardComments.SingleAsync()).Text);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("other card")]
    [InlineData("other board")]
    public async Task UpdateCommentAsync_WhenCommentDoesNotBelongToTarget_ShouldReturnNotFound(string target)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").AddCard("Other").Build();
        var card = board.GetCard("Card");
        var comment = await AddCommentAsync(card.Id, ActorUserId);
        var boardId = board.BoardId;
        var cardId = card.BoardCardId;
        var commentId = comment.Id;
        if (target == "missing") { commentId = int.MaxValue; }
        if (target == "other card") { cardId = board.GetCard("Other").BoardCardId; }
        if (target == "other board")
        {
            var other = CreateBoard("Other board").AddColumn("Todo").AddCard("Card").Build();
            boardId = other.BoardId;
            cardId = other.GetCard("Card").BoardCardId;
        }

        var result = await ResolveService<ICardCommentService>().UpdateCommentAsync(boardId, cardId, commentId, new("Replacement"), ActorUserId);

        Assert.Equal(404, result.StatusCode);
        Assert.Equal(comment.Text, (await DbContextForAssert.CardComments.SingleAsync()).Text);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4001)]
    public async Task UpdateCommentAsync_WhenTextInvalid_ShouldLeaveCommentUnchanged(int length)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        var comment = await AddCommentAsync(card.Id, ActorUserId);
        var text = new string('x', length);
        if (length == 0) { text = " \n "; }

        var result = await ResolveService<ICardCommentService>().UpdateCommentAsync(board.BoardId, card.BoardCardId, comment.Id, new(text), ActorUserId);

        Assert.Equal(400, result.StatusCode);
        Assert.Contains("text", result.ValidationErrors!.Keys);
        var stored = await DbContextForAssert.CardComments.SingleAsync();
        Assert.Equal(comment.Text, stored.Text);
        Assert.Null(stored.EditedAtUtc);
    }

    [Fact]
    public async Task UpdateCommentAsync_WithOlderDraft_ShouldUseLastSaveWins()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        var comment = await AddCommentAsync(card.Id, ActorUserId);
        var service = ResolveService<ICardCommentService>();

        var results = new List<bool>();
        foreach (var text in new[] { "Newer draft", "Older draft saved last" })
        {
            var result = await service.UpdateCommentAsync(board.BoardId, card.BoardCardId, comment.Id, new(text), ActorUserId);
            results.Add(result.Success);
        }

        Assert.All(results, Assert.True);
        Assert.Equal("Older draft saved last", (await DbContextForAssert.CardComments.SingleAsync()).Text);
    }

    private async Task<EntityCardComment> AddCommentAsync(int cardId, int? authorUserId)
    {
        var comment = new EntityCardComment { CardId = cardId, AuthorUserId = authorUserId, Text = "Original comment", PostedAtUtc = PostedAtUtc, CreatedAtUtc = PostedAtUtc };
        DbContextForArrange.CardComments.Add(comment);
        await DbContextForArrange.SaveChangesAsync();
        return comment;
    }
}
