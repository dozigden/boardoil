using BoardOil.Abstractions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Card;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardCommentDeletionServiceTests : TestBaseDb
{
    [Theory]
    [InlineData(BoardMemberRole.Owner, "self")]
    [InlineData(BoardMemberRole.Contributor, "self")]
    [InlineData(BoardMemberRole.Owner, "other")]
    [InlineData(BoardMemberRole.Owner, "missing")]
    public async Task DeleteCommentAsync_WhenAuthorOrOwner_ShouldRemoveCommentAndKeepAttachments(BoardMemberRole role, string author)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        DbContextForArrange.BoardMembers.Single(x => x.BoardId == board.BoardId).Role = role;
        var comment = await AddCommentAsync(card.Id, author);
        var attachment = new EntityCardAttachment
        {
            CardId = card.Id, State = AttachmentState.Ready, OriginalFileName = "shared.png",
            NormalisedFileName = "SHARED.PNG", ContentType = "image/png", ByteLength = 1,
            StorageKey = "shared-file", Sha256 = "hash", CreatedByUserId = ActorUserId
        };
        DbContextForArrange.CardAttachments.Add(attachment);
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<ICardCommentService>().DeleteCommentAsync(board.BoardId, card.BoardCardId, comment.Id, ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(200, result.StatusCode);
        Assert.Empty(await DbContextForAssert.CardComments.ToListAsync());
        var keptAttachment = await DbContextForAssert.CardAttachments.SingleAsync();
        Assert.Equal(attachment.Id, keptAttachment.Id);
        Assert.Equal(AttachmentState.Ready, keptAttachment.State);
        Assert.Equal(attachment.StorageKey, keptAttachment.StorageKey);
        var storedCard = await DbContextForAssert.Cards.SingleAsync();
        Assert.True(storedCard.CardUpdatedUtc > card.CardUpdatedUtc);
        var events = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
        Assert.Equal((board.BoardId, card.BoardCardId, comment.Id), Assert.Single(events.CommentDeletedEvents));
        Assert.Equal(storedCard.CardUpdatedUtc, Assert.Single(events.CardUpdatedEvents).Card.CardUpdatedUtc);
        Assert.Empty(events.AttachmentDeletedEvents);
    }

    [Theory]
    [InlineData("other")]
    [InlineData("missing")]
    public async Task DeleteCommentAsync_WhenContributorIsNotAuthor_ShouldForbid(string author)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        DbContextForArrange.BoardMembers.Single(x => x.BoardId == board.BoardId).Role = BoardMemberRole.Contributor;
        var comment = await AddCommentAsync(card.Id, author);

        var result = await ResolveService<ICardCommentService>().DeleteCommentAsync(board.BoardId, card.BoardCardId, comment.Id, ActorUserId);

        Assert.Equal(403, result.StatusCode);
        Assert.Equal(comment.Id, (await DbContextForAssert.CardComments.SingleAsync()).Id);
        Assert.Empty(Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>()).CommentDeletedEvents);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task DeleteCommentAsync_WhenRemovedOrInactive_ShouldForbidEvenWhenCommentMissing(bool inactive, bool missingComment)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        var comment = await AddCommentAsync(card.Id, "self");
        if (inactive)
        {
            DbContextForArrange.Users.Single(x => x.Id == ActorUserId).IsActive = false;
        }
        else
        {
            DbContextForArrange.BoardMembers.Remove(DbContextForArrange.BoardMembers.Single(x => x.BoardId == board.BoardId));
        }
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<ICardCommentService>().DeleteCommentAsync(board.BoardId, card.BoardCardId, missingComment ? int.MaxValue : comment.Id, ActorUserId);

        Assert.Equal(403, result.StatusCode);
        Assert.Equal(comment.Id, (await DbContextForAssert.CardComments.SingleAsync()).Id);
    }

    [Theory]
    [InlineData("card")]
    [InlineData("board")]
    public async Task DeleteCommentAsync_WithMismatchedResource_ShouldReturnNotFound(string target)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").AddCard("Other").Build();
        var card = board.GetCard("Card");
        var comment = await AddCommentAsync(card.Id, "self");
        var boardId = board.BoardId;
        var cardId = card.BoardCardId;
        var commentId = comment.Id;
        if (target == "card")
        {
            cardId = board.GetCard("Other").BoardCardId;
        }
        if (target == "board")
        {
            var other = CreateBoard("Other board").AddColumn("Todo").AddCard("Other").Build();
            boardId = other.BoardId;
            cardId = other.GetCard("Other").BoardCardId;
        }

        var result = await ResolveService<ICardCommentService>().DeleteCommentAsync(boardId, cardId, commentId, ActorUserId);

        Assert.Equal(404, result.StatusCode);
        Assert.Equal(comment.Id, (await DbContextForAssert.CardComments.SingleAsync()).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletedComment_ShouldRejectLaterUpdateButSucceedOnRepeatedDelete(bool update)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        var comment = await AddCommentAsync(card.Id, "self");
        var service = ResolveService<ICardCommentService>();
        Assert.True((await service.DeleteCommentAsync(board.BoardId, card.BoardCardId, comment.Id, ActorUserId)).Success);
        var timestampAfterDeletion = (await DbContextForAssert.Cards.AsNoTracking().SingleAsync()).CardUpdatedUtc;

        var result = update
            ? await service.UpdateCommentAsync(board.BoardId, card.BoardCardId, comment.Id, new UpdateCardCommentRequest("Old draft"), ActorUserId)
            : await service.DeleteCommentAsync(board.BoardId, card.BoardCardId, comment.Id, ActorUserId);

        Assert.Equal(update ? 404 : 200, result.StatusCode);
        Assert.Empty(await DbContextForAssert.CardComments.ToListAsync());
        Assert.Equal(timestampAfterDeletion, (await DbContextForAssert.Cards.AsNoTracking().SingleAsync()).CardUpdatedUtc);
        var events = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
        Assert.Single(events.CommentDeletedEvents);
        Assert.Single(events.CardUpdatedEvents);
        Assert.Empty(events.CommentUpdatedEvents);
    }

    [Theory]
    [InlineData(BoardMemberRole.Owner, false)]
    [InlineData(BoardMemberRole.Owner, true)]
    [InlineData(BoardMemberRole.Contributor, false)]
    [InlineData(BoardMemberRole.Contributor, true)]
    public async Task DeleteCommentAsync_WhenCommentOrCardMissing_ShouldSucceedWithoutSideEffects(BoardMemberRole role, bool missingCard)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        DbContextForArrange.BoardMembers.Single(x => x.BoardId == board.BoardId).Role = role;
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<ICardCommentService>().DeleteCommentAsync(
            board.BoardId, missingCard ? int.MaxValue : card.BoardCardId, int.MaxValue, ActorUserId);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(card.CardUpdatedUtc, (await DbContextForAssert.Cards.SingleAsync()).CardUpdatedUtc);
        var events = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
        Assert.Empty(events.CardUpdatedEvents);
        Assert.Empty(events.CommentDeletedEvents);
    }

    [Fact]
    public async Task DeletedComment_ShouldBeExcludedFromExportsAndNewArchiveSnapshots()
    {
        var board = CreateBoard("Source").AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        var comment = await AddCommentAsync(card.Id, "self");
        Assert.True((await ResolveService<ICardCommentService>().DeleteCommentAsync(board.BoardId, card.BoardCardId, comment.Id, ActorUserId)).Success);

        var exported = await ResolveService<IBoardExportService>().ExportBoardAsync(board.BoardId, ActorUserId, "test");
        Assert.True(exported.Success, exported.Message);
        await using var stream = exported.Data!.Content;
        using var bytes = new MemoryStream();
        await stream.CopyToAsync(bytes);
        var imported = await ResolveService<IBoardPackageImportService>().ImportBoardPackageAsync(new ImportBoardPackageRequest("Imported", bytes.ToArray()), ActorUserId);
        var archiveService = ResolveService<ICardArchiveService>();
        var archived = await archiveService.ArchiveCardAsync(board.BoardId, card.BoardCardId, ActorUserId);
        var detail = await archiveService.GetArchivedCardAsync(board.BoardId, card.BoardCardId, ActorUserId);

        Assert.True(imported.Success, imported.Message);
        Assert.True(archived.Success, archived.Message);
        Assert.True(detail.Success, detail.Message);
        Assert.Empty(detail.Data!.Comments);
        Assert.Empty(await DbContextForAssert.CardComments.ToListAsync());
    }

    private async Task<EntityCardComment> AddCommentAsync(int cardId, string author)
    {
        int? authorUserId = ActorUserId;
        if (author == "missing")
        {
            authorUserId = null;
        }
        if (author == "other")
        {
            var user = new EntityUser { UserName = "author", Email = "author@test", NormalisedEmail = "author@test", PasswordHash = "test", IsActive = true };
            DbContextForArrange.Users.Add(user);
            await DbContextForArrange.SaveChangesAsync();
            authorUserId = user.Id;
        }
        var comment = new EntityCardComment { CardId = cardId, AuthorUserId = authorUserId, Text = "Original comment", PostedAtUtc = DateTime.UtcNow };
        DbContextForArrange.CardComments.Add(comment);
        await DbContextForArrange.SaveChangesAsync();
        return comment;
    }
}
