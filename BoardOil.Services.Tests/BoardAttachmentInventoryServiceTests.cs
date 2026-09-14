using BoardOil.Abstractions.Attachment;
using BoardOil.Contracts.Card;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class BoardAttachmentInventoryServiceTests : TestBaseDb
{
    [Fact]
    public async Task List_ShouldIncludeAllPublishedFilesAcrossLiveAndArchivedCardsWithinBoard()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Live card").Build();
        var other = CreateBoard("Other board").AddColumn("Todo").AddCard("Other card").Build();
        var archive = new EntityArchivedCard
        {
            BoardId = board.BoardId, OriginalCardId = 846, SearchTitle = "Archived card",
            // Inventory must not need to understand archived snapshot versions.
            SnapshotJson = "{\"schema\":\"archived-card\",\"version\":999}", SearchTagsJson = "[]"
        };
        var otherArchive = new EntityArchivedCard
        {
            BoardId = other.BoardId, OriginalCardId = 846, SearchTitle = "Other archive",
            SnapshotJson = "{}", SearchTagsJson = "[]"
        };
        DbContextForArrange.ArchivedCards.AddRange(archive, otherArchive);
        var live = Attachment("notes.txt", 12, board.GetCard("Live card").Id);
        var image = Attachment("image.png", 30, board.GetCard("Live card").Id);
        image.ThumbnailStorageKey = "thumbnail-excluded-from-total";
        var archived = Attachment("report.pdf", 50);
        archived.ArchivedCard = archive;
        var foreignArchiveFile = Attachment("foreign.pdf", 900);
        foreignArchiveFile.ArchivedCard = otherArchive;
        var pending = Attachment("pending.bin", 400, live.CardId);
        pending.State = AttachmentState.Pending;
        var deleting = Attachment("deleting.bin", 800);
        deleting.State = AttachmentState.PendingDeletion;
        DbContextForArrange.CardAttachments.AddRange(live, image, archived, pending, deleting,
            Attachment("foreign.txt", 900, other.GetCard("Other card").Id), foreignArchiveFile);
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<IBoardAttachmentInventoryService>().ListAsync(board.BoardId, ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(3, result.Data!.TotalCount);
        Assert.Equal(92, result.Data.TotalByteLength);
        Assert.Equal(new[] { archived.Id, image.Id, live.Id }, result.Data.Items.Select(x => x.Id));
        var archivedItem = Assert.Single(result.Data.Items, x => x.Archived);
        Assert.Equal(846, archivedItem.CardId);
        Assert.Equal("Archived card", archivedItem.CardTitle);
        Assert.Equal("application/pdf", archivedItem.ContentType);
        Assert.Equal(archived.CreatedAtUtc, archivedItem.CreatedAtUtc);
        Assert.All(result.Data.Items.Where(x => !x.Archived), item =>
        {
            Assert.Equal(board.GetCard("Live card").BoardCardId, item.CardId);
            Assert.Equal("Live card", item.CardTitle);
        });
    }

    [Fact]
    public async Task List_WhenEmpty_ShouldReturnZeroTotals()
    {
        var board = CreateBoard().Build();

        var result = await ResolveService<IBoardAttachmentInventoryService>().ListAsync(board.BoardId, ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Empty(result.Data!.Items);
        Assert.Equal(0, result.Data.TotalCount);
        Assert.Equal(0, result.Data.TotalByteLength);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task List_WhenActorIsNotBoardOwner_ShouldDenyAccess(bool contributor)
    {
        var board = CreateBoard().Build();
        var membership = await DbContextForArrange.BoardMembers.SingleAsync(x => x.BoardId == board.BoardId);
        if (contributor) { membership.Role = BoardMemberRole.Contributor; }
        else { DbContextForArrange.BoardMembers.Remove(membership); }
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<IBoardAttachmentInventoryService>().ListAsync(board.BoardId, ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        Assert.Null(result.Data);
    }

    [Theory]
    [InlineData("name", "asc", 2, 3)]
    [InlineData("name", "desc", 3, 2)]
    [InlineData("date", "asc", 2, 0)]
    [InlineData("date", "desc", 0, 2)]
    [InlineData("size", "asc", 1, 2)]
    [InlineData("size", "desc", 2, 1)]
    public async Task List_ShouldSortBeforePagingWithStableTies(string sort, string direction, int first, int second)
    {
        var (boardId, files) = await SeedInventory();

        var result = await ResolveService<IBoardAttachmentInventoryService>().ListAsync(boardId, ActorUserId,
            new BoardAttachmentInventoryQuery(Offset: 1, Limit: 2, Sort: sort, Direction: direction));

        Assert.True(result.Success, result.Message);
        Assert.Equal(new[] { files[first].Id, files[second].Id }, result.Data!.Items.Select(x => x.Id));
        Assert.Equal(4, result.Data.TotalCount);
        Assert.Equal(80, result.Data.TotalByteLength);
        Assert.Equal(4, result.Data.MatchingCount);
        Assert.Equal(1, result.Data.Offset);
        Assert.Equal(2, result.Data.Limit);
    }

    [Theory]
    [InlineData("both", 4, 3)]
    [InlineData("live", 3, 0)]
    [InlineData("archived", 1, 3)]
    public async Task List_ShouldFilterBeforePagingWhileKeepingBoardTotals(string state, int matching, int first)
    {
        var (boardId, files) = await SeedInventory();

        var result = await ResolveService<IBoardAttachmentInventoryService>().ListAsync(boardId, ActorUserId,
            new BoardAttachmentInventoryQuery(Limit: 1, State: state));

        Assert.True(result.Success, result.Message);
        Assert.Equal(files[first].Id, Assert.Single(result.Data!.Items).Id);
        Assert.Equal(matching, result.Data.MatchingCount);
        Assert.Equal(4, result.Data.TotalCount);
        Assert.Equal(80, result.Data.TotalByteLength);
    }

    [Fact]
    public async Task List_ShouldLimitDefaultPageToFiftyAttachments()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        DbContextForArrange.CardAttachments.AddRange(Enumerable.Range(1, 51)
            .Select(index => Attachment($"{index}.txt", 1, board.GetCard("Card").Id)));
        await DbContextForArrange.SaveChangesAsync();

        var result = await ResolveService<IBoardAttachmentInventoryService>().ListAsync(board.BoardId, ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(50, result.Data!.Items.Count);
        Assert.Equal(51, result.Data.MatchingCount);
        Assert.Equal(51, result.Data.TotalCount);
        Assert.Equal(51, result.Data.TotalByteLength);
    }

    [Fact]
    public async Task List_WhenPageIsBeyondMatches_ShouldReturnEmptyPageWithTotals()
    {
        var (boardId, _) = await SeedInventory();

        var result = await ResolveService<IBoardAttachmentInventoryService>().ListAsync(boardId, ActorUserId,
            new BoardAttachmentInventoryQuery(Offset: 50, State: "archived"));

        Assert.True(result.Success, result.Message);
        Assert.Empty(result.Data!.Items);
        Assert.Equal(1, result.Data.MatchingCount);
        Assert.Equal(4, result.Data.TotalCount);
        Assert.Equal(80, result.Data.TotalByteLength);
    }

    [Theory]
    [InlineData(-1, 50, "name", "asc", "both")]
    [InlineData(0, 0, "name", "asc", "both")]
    [InlineData(0, 201, "name", "asc", "both")]
    [InlineData(0, 50, "unknown", "asc", "both")]
    [InlineData(0, 50, "name", "unknown", "both")]
    [InlineData(0, 50, "name", "asc", "unknown")]
    public async Task List_ShouldRejectInvalidQueries(int offset, int limit, string sort, string direction, string state)
    {
        var board = CreateBoard().Build();

        var result = await ResolveService<IBoardAttachmentInventoryService>().ListAsync(board.BoardId, ActorUserId,
            new BoardAttachmentInventoryQuery(offset, limit, sort, direction, state));

        Assert.Equal(400, result.StatusCode);
        Assert.False(result.Success);
    }

    private async Task<(int BoardId, EntityCardAttachment[] Files)> SeedInventory()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("One").AddCard("Two").Build();
        EntityCardAttachment[] files = [
            Attachment("zebra.txt", 30, board.GetCard("One").Id),
            Attachment("Alpha.txt", 20, board.GetCard("One").Id),
            Attachment("alpha.txt", 20, board.GetCard("Two").Id),
            Attachment("middle.pdf", 10)
        ];
        files[0].CreatedAtUtc = files[0].CreatedAtUtc.AddDays(1);
        files[3].CreatedAtUtc = files[3].CreatedAtUtc.AddDays(2);
        files[3].ArchivedCard = new EntityArchivedCard
        {
            BoardId = board.BoardId, OriginalCardId = 100, SearchTitle = "Archived",
            SnapshotJson = "{}", SearchTagsJson = "[]"
        };
        DbContextForArrange.CardAttachments.AddRange(files);
        await DbContextForArrange.SaveChangesAsync();
        return (board.BoardId, files);
    }

    private static EntityCardAttachment Attachment(string fileName, long bytes, int? cardId = null) => new()
    {
        CardId = cardId, State = AttachmentState.Ready,
        OriginalFileName = fileName, NormalisedFileName = fileName.ToUpperInvariant(),
        ContentType = fileName.EndsWith(".pdf", StringComparison.Ordinal) ? "application/pdf" : "application/octet-stream",
        ByteLength = bytes, StorageKey = Guid.NewGuid().ToString("N"),
        CreatedAtUtc = new DateTime(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc)
    };
}
