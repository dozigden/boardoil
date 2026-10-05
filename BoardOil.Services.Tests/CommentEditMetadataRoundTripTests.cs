using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Board;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CommentEditMetadataRoundTripTests : TestBaseDb
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ExportImportAndRestore_ShouldPreserveLiveAndArchivedCommentMetadata(bool edited, bool isSystem)
    {
        var board = CreateBoard("Source").AddColumn("Todo").AddCard("Live").AddCard("Archived").Build();
        var liveCard = board.GetCard("Live");
        var archivedCard = board.GetCard("Archived");
        var postedAtUtc = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc);
        DateTime? editedAtUtc = null;
        if (edited) { editedAtUtc = postedAtUtc.AddHours(1); }
        foreach (var card in new[] { liveCard, archivedCard })
        {
            DbContextForArrange.CardComments.Add(new EntityCardComment
            {
                CardId = card.Id, AuthorUserId = isSystem ? null : ActorUserId, IsSystem = isSystem, Text = $"{card.Title} comment",
                PostedAtUtc = postedAtUtc, EditedAtUtc = editedAtUtc
            });
        }
        await DbContextForArrange.SaveChangesAsync();
        var archiveService = ResolveService<ICardArchiveService>();
        var archived = await archiveService.ArchiveCardAsync(board.BoardId, archivedCard.BoardCardId, ActorUserId);
        Assert.True(archived.Success, archived.Message);

        var exported = await ResolveService<IBoardExportService>().ExportBoardAsync(board.BoardId, ActorUserId, "test");
        Assert.True(exported.Success, exported.Message);
        await using var packageStream = exported.Data!.Content;
        using var bytes = new MemoryStream();
        await packageStream.CopyToAsync(bytes);
        var imported = await ResolveService<IBoardPackageImportService>().ImportBoardPackageAsync(
            new ImportBoardPackageRequest("Imported", bytes.ToArray()), ActorUserId);
        Assert.True(imported.Success, imported.Message);
        var importedBoardId = imported.Data!.Id;
        var archivedDetail = await archiveService.GetArchivedCardAsync(importedBoardId, archivedCard.BoardCardId, ActorUserId);
        Assert.True(archivedDetail.Success, archivedDetail.Message);
        var restored = await archiveService.UnarchiveCardAsync(importedBoardId, archivedCard.BoardCardId, ActorUserId);

        Assert.True(restored.Success, restored.Message);
        var archivedComment = Assert.Single(archivedDetail.Data!.Comments);
        Assert.Equal(isSystem, archivedComment.IsSystem);
        if (isSystem)
        {
            Assert.Null(archivedComment.AuthorUserId);
            Assert.Null(archivedComment.AuthorDisplayName);
            Assert.Null(archivedComment.AuthorImageRelativePath);
        }
        Assert.Equal(editedAtUtc, archivedComment.EditedAtUtc);
        Assert.Equal(postedAtUtc, archivedComment.PostedAtUtc);
        var stored = await DbContextForAssert.CardComments
            .Where(x => x.Card.BoardId == importedBoardId)
            .OrderBy(x => x.Card.Title).ToListAsync();
        Assert.Equal(2, stored.Count);
        Assert.Equal(new[] { "Archived comment", "Live comment" }, stored.Select(x => x.Text));
        Assert.All(stored, comment =>
        {
            Assert.Equal(isSystem, comment.IsSystem);
            Assert.Equal(isSystem ? (int?)null : ActorUserId, comment.AuthorUserId);
            Assert.Equal(postedAtUtc, comment.PostedAtUtc);
            Assert.Equal(editedAtUtc, comment.EditedAtUtc);
        });
    }
}
