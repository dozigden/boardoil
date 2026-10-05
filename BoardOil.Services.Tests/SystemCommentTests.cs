using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Card;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class SystemCommentTests : TestBaseDb
{
    [Theory]
    [InlineData("", false)]
    [InlineData(",\"isSystem\":true", true)]
    public async Task RestoreStaticSnapshot_ShouldDefaultLegacyCommentsAndPreserveSystemComments(string marker, bool expectedSystem)
    {
        var board = CreateBoard().AddColumn("Todo").Build();
        var snapshot = $$$"""
            {"schema":"archived-card","version":1,"capturedAtUtc":"2026-05-01T08:00:00Z","payload":{
              "boardId":1,"originalCardId":99,"boardColumnId":1,"originalColumnName":"Todo",
              "cardTypeId":1,"cardTypeName":"Story","title":"Archived","description":"","sortKey":"A",
              "tags":[],"tagNames":[],"createdAtUtc":"2026-05-01T08:00:00Z","updatedAtUtc":"2026-05-01T08:00:00Z",
              "comments":[{"text":"Estimate: 7","createdAtUtc":"2026-05-01T08:00:00Z"
              {{{marker}}}
              }]}}
            """;
        DbContextForArrange.ArchivedCards.Add(new EntityArchivedCard
        {
            BoardId = board.BoardId, OriginalCardId = 99, SearchTitle = "Archived", SearchTagsJson = "[]",
            SnapshotJson = snapshot, ArchivedAtUtc = DateTime.UtcNow
        });
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardArchiveService>();

        var detail = await service.GetArchivedCardAsync(board.BoardId, 99, ActorUserId);
        var restored = await service.UnarchiveCardAsync(board.BoardId, 99, ActorUserId);

        Assert.True(detail.Success, detail.Message);
        Assert.Equal(expectedSystem, Assert.Single(detail.Data!.Comments).IsSystem);
        Assert.True(restored.Success, restored.Message);
        var comment = await DbContextForAssert.CardComments.SingleAsync();
        Assert.Equal(expectedSystem, comment.IsSystem);
        Assert.Null(comment.AuthorUserId);
        Assert.Equal("Estimate: 7", comment.Text);
        Assert.Equal(new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc), comment.PostedAtUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetComments_ShouldDistinguishSystemFromMissingAuthor(bool isSystem)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        DbContextForArrange.CardComments.Add(new EntityCardComment
        {
            CardId = card.Id, Text = "Recorded values", IsSystem = isSystem,
            PostedAtUtc = DateTime.UtcNow
        });
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<ICardCommentService>().GetCommentsAsync(board.BoardId, card.BoardCardId, ActorUserId);

        Assert.True(result.Success, result.Message);
        var comment = Assert.Single(result.Data!);
        Assert.Equal(isSystem, comment.IsSystem);
        Assert.Null(comment.AuthorUserId);
        Assert.Null(comment.AuthorImageRelativePath);
        Assert.Equal(isSystem ? null : "Unknown user", comment.AuthorDisplayName);
    }

    [Theory]
    [InlineData(BoardMemberRole.Owner, false)]
    [InlineData(BoardMemberRole.Owner, true)]
    [InlineData(BoardMemberRole.Contributor, false)]
    [InlineData(BoardMemberRole.Contributor, true)]
    public async Task SystemComments_ShouldOnlyAllowOwnerDeletion(BoardMemberRole role, bool delete)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        DbContextForArrange.BoardMembers.Single(x => x.BoardId == board.BoardId).Role = role;
        var comment = new EntityCardComment
        {
            CardId = card.Id, Text = "Recorded values", IsSystem = true,
            PostedAtUtc = DateTime.UtcNow
        };
        DbContextForArrange.CardComments.Add(comment);
        await DbContextForArrange.SaveChangesAsync();
        var service = ResolveService<ICardCommentService>();

        if (delete)
        {
            var result = await service.DeleteCommentAsync(board.BoardId, card.BoardCardId, comment.Id, ActorUserId);
            Assert.Equal(role == BoardMemberRole.Owner ? 200 : 403, result.StatusCode);
        }
        else
        {
            var result = await service.UpdateCommentAsync(board.BoardId, card.BoardCardId, comment.Id,
                new UpdateCardCommentRequest("Changed"), ActorUserId);
            Assert.Equal(403, result.StatusCode);
        }

        if (delete && role == BoardMemberRole.Owner)
        {
            Assert.Empty(await DbContextForAssert.CardComments.ToListAsync());
        }
        else
        {
            Assert.Equal("Recorded values", (await DbContextForAssert.CardComments.SingleAsync()).Text);
            Assert.Equal(card.CardUpdatedUtc, (await DbContextForAssert.Cards.SingleAsync()).CardUpdatedUtc);
        }
    }
}
