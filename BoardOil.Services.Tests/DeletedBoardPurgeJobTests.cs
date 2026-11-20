using System.Text.Json;
using BoardOil.Abstractions.Attachment;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.Jobs;
using BoardOil.Contracts.Jobs;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Attachment;
using BoardOil.Services.Jobs;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class DeletedBoardPurgeJobTests : TestBaseDb, IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Cutoff = Now.AddDays(-10);
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boardoil-board-purge-" + Guid.NewGuid().ToString("N"));
    private Action<string>? _beforeDeleteFile;

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(new FixedClock());
        services.AddLogging();
        services.AddSingleton(new AttachmentStorageOptions { RootPath = _root });
        services.AddSingleton<IAttachmentStorageService>(provider =>
            new TestStorage(new LocalAttachmentStorageService(provider.GetRequiredService<AttachmentStorageOptions>()),
                key => _beforeDeleteFile?.Invoke(key)));
    }

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (Directory.Exists(_root)) { Directory.Delete(_root, true); }
    }

    [Fact]
    public async Task Purge_ShouldRemoveBoardsAtOrBeforeCutoffWithLiveAndArchivedFiles()
    {
        var old = CreateBoard("Older").AddColumn("Todo").AddCard("Live").AddCard("Archived").Build();
        var exact = CreateBoard("At cutoff").Build();
        var recent = CreateBoard("Recent").Build();
        var active = CreateBoard("Active").AddColumn("Todo").AddCard("Kept").Build();
        var live = await AddAttachmentAsync(old, "Live");
        var archived = await AddAttachmentAsync(old, "Archived");
        var kept = await AddAttachmentAsync(active, "Kept");
        var archive = await ResolveService<ICardArchiveService>()
            .ArchiveCardAsync(old.BoardId, old.GetCard("Archived").BoardCardId, ActorUserId);
        Assert.True(archive.Success, archive.Message);
        Assert.True(await DbContextForAssert.CardAttachments.AnyAsync(x => x.Id == archived.Id && x.ArchivedCardId != null));
        SetDeletionTime(old.BoardId, Cutoff.AddTicks(-1));
        SetDeletionTime(exact.BoardId, Cutoff);
        SetDeletionTime(recent.BoardId, Cutoff.AddTicks(1));

        var result = await DispatchAsync();

        Assert.True(result.Success);
        using var summary = JsonDocument.Parse(result.ResultJson);
        Assert.Equal(10, summary.RootElement.GetProperty("retentionDays").GetInt32());
        Assert.Equal(Cutoff, summary.RootElement.GetProperty("cutoffUtc").GetDateTime());
        Assert.Equal(2, summary.RootElement.GetProperty("queuedCount").GetInt32());
        var queued = await DbContextForAssert.Jobs.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        Assert.All(queued, job =>
        {
            Assert.Equal(PurgeBoardJobHandler.JobType, job.Type);
            Assert.Equal(JobStatus.Pending, job.Status);
        });
        Assert.Equal([old.BoardId, exact.BoardId],
            queued.Select(x => JsonSerializer.Deserialize<PurgeBoardJobPayload>(x.PayloadJson)!.BoardId));
        Assert.Equal(4, await DbContextForAssert.Boards.CountAsync());

        var runner = ResolveService<IJobRunner>();
        Assert.True(await runner.RunNextDueAsync());
        Assert.True(await runner.RunNextDueAsync());
        Assert.Equal([recent.BoardId, active.BoardId],
            await DbContextForAssert.Boards.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync());
        Assert.False(await DbContextForAssert.BoardMembers.AnyAsync(x => x.BoardId == old.BoardId || x.BoardId == exact.BoardId));
        Assert.False(await DbContextForAssert.Columns.AnyAsync(x => x.BoardId == old.BoardId));
        Assert.False(await DbContextForAssert.Cards.AnyAsync(x => x.BoardId == old.BoardId));
        Assert.False(await DbContextForAssert.ArchivedCards.AnyAsync(x => x.BoardId == old.BoardId));
        Assert.Equal(kept.Id, (await DbContextForAssert.CardAttachments.SingleAsync()).Id);
        Assert.Equal(404, (await ResolveService<ISystemBoardService>().RestoreBoardAsync(old.BoardId)).StatusCode);
        foreach (var deleted in new[] { live, archived })
        {
            Assert.False(FileExists(deleted.StorageKey));
            Assert.False(FileExists(deleted.ThumbnailStorageKey!));
        }
        Assert.True(FileExists(kept.StorageKey));
        Assert.True(FileExists(kept.ThumbnailStorageKey!));
    }

    [Fact]
    public async Task Purge_WhenLastBoardIsRemoved_ShouldKeepInstallationEmptyOnNextBootstrap()
    {
        var board = CreateBoard().Build();
        SetDeletionTime(board.BoardId, Cutoff);
        await ResolveService<IBoardBootstrapService>().EnsureDefaultBoardAsync();

        Assert.True((await PurgeAsync(board.BoardId)).Success);
        await ResolveService<IBoardBootstrapService>().EnsureDefaultBoardAsync();

        Assert.Empty(await DbContextForAssert.Boards.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Purge_WhenBoardChangesAfterDispatch_ShouldCheckCurrentSoftDeleteState(bool deleteAgain)
    {
        var board = CreateBoard().Build();
        SetDeletionTime(board.BoardId, Cutoff);
        Assert.True((await DispatchAsync()).Success);
        Assert.True((await ResolveService<ISystemBoardService>().RestoreBoardAsync(board.BoardId)).Success);
        if (deleteAgain) { SetDeletionTime(board.BoardId, Now); }

        Assert.True(await ResolveService<IJobRunner>().RunNextDueAsync());

        Assert.Equal(JobStatus.Completed, await DbContextForAssert.Jobs.Select(x => x.Status).SingleAsync());
        Assert.Equal(!deleteAgain, await DbContextForAssert.Boards.AnyAsync(x => x.Id == board.BoardId));
    }

    [Fact]
    public async Task Purge_WhenOneBoardTransactionFails_ShouldStillPurgeTheNextBoard()
    {
        var failed = CreateBoard("Fails purge").AddColumn("Todo").AddCard("Card").Build();
        var next = CreateBoard("Eligible").Build();
        var attachment = await AddAttachmentAsync(failed, "Card");
        SetDeletionTime(failed.BoardId, Cutoff);
        SetDeletionTime(next.BoardId, Cutoff);
        await DbContextForArrange.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER fail_board_purge BEFORE DELETE ON Boards
            WHEN OLD.Name = 'Fails purge'
            BEGIN
                SELECT RAISE(ABORT, 'Test board deletion failure');
            END;
            """);
        Assert.True((await DispatchAsync()).Success);
        var runner = ResolveService<IJobRunner>();

        Assert.True(await runner.RunNextDueAsync());
        Assert.True(await runner.RunNextDueAsync());

        Assert.Equal([JobStatus.Failed, JobStatus.Completed],
            await DbContextForAssert.Jobs.OrderBy(x => x.Id).Select(x => x.Status).ToListAsync());
        Assert.Equal(failed.BoardId, await DbContextForAssert.Boards.Select(x => x.Id).SingleAsync());
        Assert.Equal(AttachmentState.Ready, await DbContextForAssert.CardAttachments.Select(x => x.State).SingleAsync());
        Assert.True(FileExists(attachment.StorageKey));
        Assert.True(FileExists(attachment.ThumbnailStorageKey!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Purge_WhenThumbnailDeletionIsInterrupted_ShouldLeaveFilesForStartupCleanup(bool cancelJob)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var attachment = await AddAttachmentAsync(board, "Card");
        SetDeletionTime(board.BoardId, Cutoff);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _beforeDeleteFile = key =>
        {
            if (key != attachment.ThumbnailStorageKey) { return; }
            if (cancelJob)
            {
                cancellation.Cancel();
                cancellation.Token.ThrowIfCancellationRequested();
            }
            throw new IOException("Thumbnail is temporarily unavailable.");
        };

        if (cancelJob)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PurgeAsync(board.BoardId, cancellation.Token));
        }
        else
        {
            var failed = await PurgeAsync(board.BoardId, cancellation.Token);
            Assert.False(failed.Success);
        }

        Assert.Empty(await DbContextForAssert.Boards.ToListAsync());
        var pending = await DbContextForAssert.CardAttachments.AsNoTracking().SingleAsync();
        Assert.Equal(AttachmentState.PendingDeletion, pending.State);
        Assert.Null(pending.CardId);
        Assert.False(FileExists(attachment.StorageKey));
        Assert.True(FileExists(attachment.ThumbnailStorageKey!));

        _beforeDeleteFile = null;
        Assert.True((await DispatchAsync()).Success);
        Assert.Empty(await DbContextForAssert.Jobs.ToListAsync());
        Assert.True(FileExists(attachment.ThumbnailStorageKey!));

        Assert.Equal(1, await ResolveService<CardAttachmentService>().CleanupAtStartupAsync());
        Assert.False(FileExists(attachment.ThumbnailStorageKey!));
        Assert.Empty(await DbContextForAssert.CardAttachments.ToListAsync());
    }

    private async Task<EntityCardAttachment> AddAttachmentAsync(FluentBoardBuilder board, string cardTitle)
    {
        using var content = new MemoryStream([1, 2, 3]);
        var result = await ResolveService<ICardAttachmentService>().UploadAsync(
            board.BoardId, board.GetCard(cardTitle).BoardCardId, ActorUserId, "file.bin", null, content);
        Assert.True(result.Success, result.Message);
        var attachment = await DbContextForArrange.CardAttachments.SingleAsync(x => x.Id == result.Data!.Id);
        attachment.ThumbnailStorageKey = Guid.NewGuid().ToString("N");
        using (var thumbnail = ResolveService<IAttachmentStorageService>().Create(attachment.ThumbnailStorageKey))
        {
            thumbnail.WriteByte(1);
        }
        await DbContextForArrange.SaveChangesAsync();
        return attachment;
    }

    private void SetDeletionTime(int boardId, DateTime? deletedAtUtc) =>
        DbContextForArrange.Boards.Where(x => x.Id == boardId)
            .ExecuteUpdate(update => update.SetProperty(x => x.DeletedAtUtc, deletedAtUtc));

    private bool FileExists(string key) => File.Exists(Path.Combine(_root, key[..2], key));

    private Task<JobHandlerResult> DispatchAsync() =>
        RunHandlerAsync(MaintenanceJobTypes.DeletedBoardPurge, "{}");

    private Task<JobHandlerResult> PurgeAsync(int boardId, CancellationToken? cancellationToken = null) =>
        RunHandlerAsync(PurgeBoardJobHandler.JobType, JsonSerializer.Serialize(new PurgeBoardJobPayload(boardId)), cancellationToken);

    private Task<JobHandlerResult> RunHandlerAsync(string jobType, string payload, CancellationToken? cancellationToken = null)
    {
        var handlers = ResolveService<IEnumerable<IJobHandler>>();
        var handler = handlers.Single(x => x.Type == jobType);
        return handler.HandleAsync(new JobContext(1, jobType, payload),
            cancellationToken ?? TestContext.Current.CancellationToken);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class TestStorage(IAttachmentStorageService inner, Action<string> beforeDelete) : IAttachmentStorageService
    {
        public Stream Create(string storageKey) => inner.Create(storageKey);
        public Stream OpenRead(string storageKey) => inner.OpenRead(storageKey);
        public void Delete(string storageKey)
        {
            beforeDelete(storageKey);
            inner.Delete(storageKey);
        }
    }
}
