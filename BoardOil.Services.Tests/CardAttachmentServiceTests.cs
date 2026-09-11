using BoardOil.Abstractions;
using BoardOil.Abstractions.Attachment;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.Column;
using BoardOil.Contracts.Card;
using BoardOil.Services.Attachment;
using BoardOil.Services.Card;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BoardOil.Data.Abstractions.Entities;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class CardAttachmentServiceTests : TestBaseDb, IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boardoil-attachment-service-" + Guid.NewGuid().ToString("N"));
    protected override void ConfigureTestServices(IServiceCollection services) =>
        services.AddSingleton(new AttachmentStorageOptions { RootPath = _root });
    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (Directory.Exists(_root)) { Directory.Delete(_root, true); }
    }

    [Fact]
    public async Task Upload_ShouldPublishReadyMetadata()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");

        var result = await ResolveService<ICardAttachmentService>().UploadAsync(board.BoardId, card.BoardCardId, ActorUserId,
            "original.unknown", null, new MemoryStream([0, 255, 3]));

        Assert.True(result.Success, result.Message);
        var stored = await DbContextForAssert.CardAttachments.SingleAsync();
        Assert.Equal(card.Id, stored.CardId);
        Assert.Equal(AttachmentState.Ready, stored.State);
        Assert.Null(stored.ArchivedCardId);
        Assert.Equal("original.unknown", stored.OriginalFileName);
        Assert.Equal("application/octet-stream", stored.ContentType);
        Assert.Empty(await DbContextForAssert.TemporaryBoardPackages.ToListAsync());
        var events = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
        Assert.Equal((board.BoardId, card.BoardCardId, result.Data!), Assert.Single(events.AttachmentAddedEvents));
        Assert.Empty(events.AttachmentDeletedEvents);
        Assert.Empty(events.ResyncRequestedBoardIds);
    }

    [Theory]
    [InlineData("report.pdf", "report.pdf")]
    [InlineData("Report.PDF", "report.pdf")]
    [InlineData("résumé.txt", "RÉSUMÉ.TXT")]
    public async Task Upload_WhenFilenameExistsIgnoringCase_ShouldRejectWithoutChangingOriginal(string originalName, string duplicateName)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        var service = ResolveService<ICardAttachmentService>();
        var original = await service.UploadAsync(board.BoardId, card.BoardCardId, ActorUserId, originalName, null, new MemoryStream([1]));

        var result = await service.UploadAsync(board.BoardId, card.BoardCardId, ActorUserId, duplicateName, null, new MemoryStream([2]));

        Assert.Equal(409, result.StatusCode);
        Assert.Contains("already exists", result.Message);
        var retained = await DbContextForAssert.CardAttachments.SingleAsync();
        Assert.Equal(original.Data!.Id, retained.Id);
        Assert.Equal(originalName, retained.OriginalFileName);
        Assert.Single(Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories));
        Assert.Single(Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>()).AttachmentAddedEvents);
    }

    [Fact]
    public async Task Upload_WhenSameFilenameIsStillUploading_ShouldRejectSecondUpload()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        var service = ResolveService<ICardAttachmentService>();
        await using var input = new BeforeReadStream(async () =>
        {
            var second = await service.UploadAsync(board.BoardId, card.BoardCardId, ActorUserId, "FILE.BIN", null, new MemoryStream([2]));
            Assert.Equal(409, second.StatusCode);
        });

        var first = await service.UploadAsync(board.BoardId, card.BoardCardId, ActorUserId, "file.bin", null, input);

        Assert.True(first.Success, first.Message);
        Assert.Equal(AttachmentState.Ready, (await DbContextForAssert.CardAttachments.SingleAsync()).State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Database_ShouldEnforceUniqueFilenameForLiveAndArchivedOwners(bool archived)
    {
        var (boardId, cardId, _) = await ArrangeAttachment();
        if (archived)
        {
            Assert.True((await ResolveService<ICardArchiveService>().ArchiveCardAsync(boardId, cardId, ActorUserId)).Success);
        }
        var original = await DbContextForArrange.CardAttachments.AsNoTracking().SingleAsync();
        original.Id = 0;
        original.StorageKey = Guid.NewGuid().ToString("N");
        original.OriginalFileName = original.OriginalFileName.ToUpperInvariant();
        DbContextForArrange.CardAttachments.Add(original);

        await Assert.ThrowsAsync<DbUpdateException>(() => DbContextForArrange.SaveChangesAsync());
    }

    [Fact]
    public async Task Delete_ShouldPublishAttachmentDeletedWithoutBoardResync()
    {
        var (boardId, cardId, attachment) = await ArrangeAttachment();

        var result = await ResolveService<ICardAttachmentService>().DeleteAsync(boardId, cardId, attachment.Id, ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Empty(await DbContextForAssert.CardAttachments.ToListAsync());
        var events = Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>());
        Assert.Equal((boardId, cardId, attachment.Id), Assert.Single(events.AttachmentDeletedEvents));
        Assert.Empty(events.ResyncRequestedBoardIds);
    }

    [Fact]
    public async Task Archive_ShouldTransferOwnerAndKeepAttachmentDownloadable()
    {
        var (boardId, cardId, attachment) = await ArrangeAttachment();

        var result = await ResolveService<ICardArchiveService>().ArchiveCardAsync(boardId, cardId, ActorUserId);

        Assert.True(result.Success, result.Message);
        var stored = await DbContextForAssert.CardAttachments.SingleAsync();
        Assert.Equal(attachment.Id, stored.Id);
        Assert.Null(stored.CardId);
        Assert.NotNull(stored.ArchivedCardId);
        var download = await ResolveService<ICardAttachmentService>().DownloadAsync(boardId, attachment.Id, ActorUserId);
        Assert.True(download.Success, download.Message);
        await using var content = download.Data!.Content;
        Assert.Equal(3, content.Length);
    }

    [Fact]
    public async Task Restore_ShouldTransferSameAttachmentBackToLiveCard()
    {
        var (boardId, cardId, attachment) = await ArrangeAttachment();
        var archive = ResolveService<ICardArchiveService>();
        Assert.True((await archive.ArchiveCardAsync(boardId, cardId, ActorUserId)).Success);

        var result = await archive.UnarchiveCardAsync(boardId, cardId, ActorUserId);

        Assert.True(result.Success, result.Message);
        var stored = await DbContextForAssert.CardAttachments.SingleAsync();
        Assert.Equal(attachment.Id, stored.Id);
        Assert.NotNull(stored.CardId);
        Assert.Null(stored.ArchivedCardId);
        Assert.Empty(await DbContextForAssert.TemporaryBoardPackages.ToListAsync());
    }

    [Fact]
    public async Task Duplicate_ShouldPreserveDraftAndCreateIndependentFiles()
    {
        var (boardId, cardId, _) = await ArrangeAttachment();

        var result = await ResolveService<ICardService>().DuplicateCardAsync(boardId, cardId,
            new CreateCardRequest(null, "Edited duplicate", "edited description", []), ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal("Edited duplicate", result.Data!.Title);
        Assert.Equal("edited description", result.Data.Description);
        var items = await DbContextForAssert.CardAttachments.OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(2, items.Count);
        Assert.NotEqual(items[0].CardId, items[1].CardId);
        Assert.NotEqual(items[0].StorageKey, items[1].StorageKey);
        Assert.Equal(items[0].Sha256, items[1].Sha256);
        Assert.Equal(items[0].OriginalFileName, items[1].OriginalFileName);
        Assert.Empty(await DbContextForAssert.TemporaryBoardPackages.ToListAsync());
    }

    [Theory]
    [InlineData("title")]
    [InlineData("column")]
    [InlineData("type")]
    public async Task Duplicate_WhenDraftIsInvalid_ShouldNotPrepareAnyCopies(string invalidField)
    {
        var (boardId, cardId, _) = await ArrangeAttachment();
        var request = new CreateCardRequest(null, "Duplicate", "", []);
        request = invalidField switch
        {
            "title" => request with { Title = "" },
            "column" => request with { BoardColumnId = int.MaxValue },
            _ => request with { CardTypeId = int.MaxValue }
        };

        var result = await ResolveService<ICardService>().DuplicateCardAsync(boardId, cardId, request, ActorUserId);

        Assert.Equal(400, result.StatusCode);
        Assert.Single(await DbContextForAssert.CardAttachments.ToListAsync());
        Assert.Single(Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories));
        Assert.Single(await DbContextForAssert.Cards.ToListAsync());
    }

    [Fact]
    public async Task Duplicate_AfterUploadLimitIsLowered_ShouldStillCopyExistingAttachments()
    {
        var (boardId, cardId, _) = await ArrangeAttachment();
        var provider = ResolveService<IServiceProvider>();
        var attachments = ActivatorUtilities.CreateInstance<CardAttachmentService>(provider,
            new AttachmentStorageOptions { RootPath = _root, MaxUploadByteLength = 1 });
        var create = ActivatorUtilities.CreateInstance<CreateCardService>(provider, attachments);
        var service = ActivatorUtilities.CreateInstance<CardService>(provider, create);

        var result = await service.DuplicateCardAsync(boardId, cardId, new CreateCardRequest(null, "Duplicate", "", []), ActorUserId);

        Assert.True(result.Success, result.Message);
        var records = await DbContextForAssert.CardAttachments.OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(2, records.Count);
        Assert.Equal(3, records[1].ByteLength);
        Assert.Equal(records[0].Sha256, records[1].Sha256);
        Assert.NotEqual(records[0].StorageKey, records[1].StorageKey);
    }

    [Fact]
    public async Task Duplicate_WhenSourceFileIsMissing_ShouldNotCreatePartialCard()
    {
        var (boardId, cardId, _) = await ArrangeAttachment();
        var key = await DbContextForArrange.CardAttachments.Select(x => x.StorageKey).SingleAsync();
        ResolveService<IAttachmentStorageService>().Delete(key);

        var result = await ResolveService<ICardService>().DuplicateCardAsync(boardId, cardId,
            new CreateCardRequest(null, "Duplicate", "", []), ActorUserId);

        Assert.False(result.Success);
        Assert.Single(await DbContextForAssert.Cards.ToListAsync());
        Assert.Single(await DbContextForAssert.CardAttachments.ToListAsync());
    }

    [Theory]
    [InlineData("card")]
    [InlineData("bulk")]
    [InlineData("column")]
    [InlineData("board")]
    [InlineData("archived-board")]
    public async Task ParentDeletion_ShouldQueueAndRemoveOwnedFiles(string operation)
    {
        var (boardId, cardId, _) = await ArrangeAttachment();
        var columnId = await DbContextForArrange.Cards.Select(x => x.BoardColumnId).SingleAsync();
        if (operation == "archived-board")
        {
            Assert.True((await ResolveService<ICardArchiveService>().ArchiveCardAsync(boardId, cardId, ActorUserId)).Success);
        }

        var result = operation switch
        {
            "card" => await ResolveService<ICardService>().DeleteCardAsync(boardId, cardId, ActorUserId),
            "bulk" => await ResolveService<BulkDeleteCardsService>().ExecuteAsync(boardId, new([cardId]), ActorUserId),
            "column" => await ResolveService<IColumnService>().DeleteColumnAsync(boardId, columnId, ActorUserId),
            _ => await ResolveService<IBoardService>().DeleteBoardAsync(boardId, ActorUserId)
        };

        Assert.True(result.Success, result.Message);
        Assert.Empty(await DbContextForAssert.CardAttachments.ToListAsync());
        Assert.Empty(await DbContextForAssert.TemporaryBoardPackages.ToListAsync());
        Assert.Empty(Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Download_WhenWrongBoard_ShouldNotExposeFile()
    {
        var (_, _, attachment) = await ArrangeAttachment();
        var other = CreateBoard("Other").AddColumn("Todo").Build();

        var result = await ResolveService<ICardAttachmentService>().DownloadAsync(other.BoardId, attachment.Id, ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Delete_WhenArchived_ShouldRequireRestore()
    {
        var (boardId, cardId, attachment) = await ArrangeAttachment();
        Assert.True((await ResolveService<ICardArchiveService>().ArchiveCardAsync(boardId, cardId, ActorUserId)).Success);

        var result = await ResolveService<ICardAttachmentService>().DeleteAsync(boardId, cardId, attachment.Id, ActorUserId);

        Assert.False(result.Success);
        Assert.Single(await DbContextForAssert.CardAttachments.ToListAsync());
        Assert.Empty(Assert.IsType<TestBoardEvents>(ResolveService<IBoardEvents>()).AttachmentDeletedEvents);
    }

    private async Task<(int BoardId, int CardId, CardAttachmentDto Attachment)> ArrangeAttachment()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var cardId = board.GetCard("Card").BoardCardId;
        var uploaded = await ResolveService<ICardAttachmentService>().UploadAsync(board.BoardId, cardId, ActorUserId,
            "file.bin", "application/octet-stream", new MemoryStream([1, 2, 3]));
        Assert.True(uploaded.Success, uploaded.Message);
        return (board.BoardId, cardId, uploaded.Data!);
    }

    [Fact]
    public async Task Transfer_ShouldRetainAttachmentAndRequireDestinationBoardAccess()
    {
        var (boardId, cardId, attachment) = await ArrangeAttachment();
        var destination = CreateBoard("Destination").AddColumn("Todo").Build();
        var transferred = await ResolveService<ICardService>().TransferCardAsync(boardId, cardId,
            new TransferCardRequest(destination.BoardId, destination.GetColumn("Todo").Id, CardTransferPolicies.DestinationDefaults), ActorUserId);

        Assert.True(transferred.Success, transferred.Message);
        var service = ResolveService<ICardAttachmentService>();
        Assert.Equal(404, (await service.DownloadAsync(boardId, attachment.Id, ActorUserId)).StatusCode);
        var download = await service.DownloadAsync(destination.BoardId, attachment.Id, ActorUserId);
        Assert.True(download.Success, download.Message);
        await download.Data!.Content.DisposeAsync();
        Assert.Equal(attachment.Id, Assert.Single((await service.ListAsync(destination.BoardId, transferred.Data!.Card.Id, false, ActorUserId)).Data!.Items).Id);
        await DbContextForArrange.BoardMembers.Where(x => x.BoardId == destination.BoardId).ExecuteDeleteAsync();
        Assert.Equal(403, (await service.DownloadAsync(destination.BoardId, attachment.Id, ActorUserId)).StatusCode);
    }

    [Fact]
    public async Task NonMember_ShouldNotListUploadDownloadOrDelete()
    {
        var (boardId, cardId, attachment) = await ArrangeAttachment();
        await DbContextForArrange.BoardMembers.ExecuteDeleteAsync();
        var service = ResolveService<ICardAttachmentService>();

        Assert.Equal(403, (await service.ListAsync(boardId, cardId, false, ActorUserId)).StatusCode);
        Assert.Equal(403, (await service.UploadAsync(boardId, cardId, ActorUserId, "file", null, new MemoryStream([1]))).StatusCode);
        Assert.Equal(403, (await service.DownloadAsync(boardId, attachment.Id, ActorUserId)).StatusCode);
        Assert.Equal(403, (await service.DeleteAsync(boardId, cardId, attachment.Id, ActorUserId)).StatusCode);
        Assert.Single(await DbContextForAssert.CardAttachments.ToListAsync());
        Assert.Empty(await DbContextForAssert.TemporaryBoardPackages.ToListAsync());
    }

    [Fact]
    public async Task Upload_WhenCardArchivedDuringCopy_ShouldRejectPublication()
    {
        var (boardId, cardId, _) = await ArrangeAttachment();
        await using var input = new BeforeReadStream(async () =>
        {
            var archived = await ResolveService<ICardArchiveService>().ArchiveCardAsync(boardId, cardId, ActorUserId);
            Assert.True(archived.Success, archived.Message);
        });

        var result = await ResolveService<ICardAttachmentService>().UploadAsync(boardId, cardId, ActorUserId, "late.bin", null, input);

        Assert.Equal(409, result.StatusCode);
        Assert.Single(await DbContextForAssert.CardAttachments.Where(x => x.State == AttachmentState.Ready).ToListAsync());
        Assert.Single(await DbContextForAssert.CardAttachments.Where(x => x.State == AttachmentState.PendingDeletion).ToListAsync());
    }

    [Fact]
    public async Task Duplicate_WhenDatabaseRejectsCard_ShouldRollbackPublicationAndLeavePendingRecord()
    {
        var (boardId, cardId, _) = await ArrangeAttachment();
        await DbContextForArrange.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER reject_duplicate BEFORE INSERT ON Cards WHEN NEW.Title = 'Rejected'
            BEGIN SELECT RAISE(ABORT, 'Simulated persistence failure'); END;
            """);

        await Assert.ThrowsAsync<DbUpdateException>(() => ResolveService<ICardService>().DuplicateCardAsync(boardId, cardId,
            new CreateCardRequest(null, "Rejected", "", []), ActorUserId));

        Assert.Single(await DbContextForAssert.Cards.ToListAsync());
        Assert.Single(await DbContextForAssert.CardAttachments.Where(x => x.State == AttachmentState.Ready).ToListAsync());
        Assert.Single(await DbContextForAssert.CardAttachments.Where(x => x.State == AttachmentState.Pending).ToListAsync());
    }

    [Fact]
    public async Task PendingUpload_ShouldNotBeListedDownloadedOrCopiedBeforePublication()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        var pending = await ResolveService<CardAttachmentService>().PrepareAsync(new MemoryStream([1]),
            "pending.bin", null, ActorUserId, card.Id);
        var service = ResolveService<ICardAttachmentService>();

        var listed = await service.ListAsync(board.BoardId, card.BoardCardId, false, ActorUserId);
        var download = await service.DownloadAsync(board.BoardId, pending.Id, ActorUserId);
        var duplicate = await ResolveService<ICardService>().DuplicateCardAsync(board.BoardId, card.BoardCardId,
            new CreateCardRequest(null, "Duplicate", "", []), ActorUserId);

        Assert.Empty(listed.Data!.Items);
        Assert.Equal(404, download.StatusCode);
        Assert.True(duplicate.Success, duplicate.Message);
        Assert.Single(await DbContextForAssert.CardAttachments.ToListAsync());
        Assert.Empty((await service.ListAsync(board.BoardId, duplicate.Data!.Id, false, ActorUserId)).Data!.Items);
    }

    [Fact]
    public async Task ParentDeletion_DuringUpload_ShouldLeaveTrackedFileForStartupAndRejectPublication()
    {
        var (boardId, cardId, _) = await ArrangeAttachment();
        await using var input = new BeforeReadStream(async () =>
        {
            Assert.True((await ResolveService<ICardService>().DeleteCardAsync(boardId, cardId, ActorUserId)).Success);
        });

        var result = await ResolveService<ICardAttachmentService>().UploadAsync(boardId, cardId, ActorUserId, "late.bin", null, input);

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(AttachmentState.PendingDeletion, (await DbContextForAssert.CardAttachments.SingleAsync()).State);
        Assert.Equal(1, await ResolveService<CardAttachmentService>().CleanupAtStartupAsync());
        Assert.Empty(Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories));
    }

    private sealed class BeforeReadStream(Func<Task> beforeRead) : MemoryStream([1, 2, 3])
    {
        private bool _called;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_called) { _called = true; await beforeRead(); }
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }
}
