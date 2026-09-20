using System.Security.Cryptography;
using BoardOil.Abstractions.Attachment;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Attachment;
using BoardOil.Services.Board;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class AttachmentStorageTests : TestBaseDb, IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boardoil-attachment-tests-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        var options = new AttachmentStorageOptions { RootPath = _root, MaxUploadByteLength = 1024 };
        services.AddSingleton(options);
        services.AddSingleton<IAttachmentStorageService>(new TestStorage(new LocalAttachmentStorageService(options)));
    }

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (Directory.Exists(_root)) { Directory.Delete(_root, true); }
    }

    [Fact]
    public async Task Prepare_ShouldCommitPendingRecordBeforeCreatingFileAndPreserveBytes()
    {
        var content = RandomNumberGenerator.GetBytes(1024);
        var storage = Assert.IsType<TestStorage>(ResolveService<IAttachmentStorageService>());
        storage.BeforeCreate = key =>
        {
            var pending = DbContextForAssert.CardAttachments.AsNoTracking().Single();
            Assert.Equal(key, pending.StorageKey);
            Assert.Equal(AttachmentState.Pending, pending.State);
            Assert.Equal("file.bin", pending.OriginalFileName);
        };

        var prepared = await Prepare(content);

        await using var stored = ResolveService<IAttachmentStorageService>().OpenRead(prepared.StorageKey);
        using var downloaded = new MemoryStream();
        await stored.CopyToAsync(downloaded);
        Assert.Equal(content, downloaded.ToArray());
        Assert.Equal(1024, prepared.ByteLength);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(content)), prepared.Sha256);
        Assert.Equal(prepared.Id, (await DbContextForAssert.CardAttachments.SingleAsync()).Id);
        Assert.Empty(await DbContextForAssert.TemporaryBoardPackages.ToListAsync());
    }

    [Fact]
    public async Task Prepare_WhenTooLarge_ShouldKeepQueryableDeletionRecordForStartup()
    {
        await Assert.ThrowsAsync<AttachmentSizeException>(() => Prepare(new byte[1025]));

        Assert.Equal(AttachmentState.PendingDeletion, (await DbContextForAssert.CardAttachments.SingleAsync()).State);
        Assert.Equal(1, await ResolveService<CardAttachmentService>().CleanupAtStartupAsync());
        Assert.Empty(await DbContextForAssert.CardAttachments.ToListAsync());
    }

    [Fact]
    public async Task Prepare_WhenFileCreationFails_ShouldLeaveDeletionRecordWithoutFile()
    {
        Assert.IsType<TestStorage>(ResolveService<IAttachmentStorageService>()).FailCreate = true;

        await Assert.ThrowsAsync<IOException>(() => Prepare([1]));

        Assert.Equal(AttachmentState.PendingDeletion, (await DbContextForAssert.CardAttachments.SingleAsync()).State);
        Assert.Equal(1, await ResolveService<CardAttachmentService>().CleanupAtStartupAsync());
    }

    [Fact]
    public async Task Prepare_ShouldAcceptEmptyFiles()
    {
        var prepared = await Prepare([]);
        Assert.Equal(0, prepared.ByteLength);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData([])), prepared.Sha256);
    }

    [Fact]
    public async Task Startup_ShouldRemovePendingFilesButKeepReadyAttachments()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Ready").Build();
        var service = ResolveService<ICardAttachmentService>();
        var ready = await service.UploadAsync(board.BoardId, board.GetCard("Ready").BoardCardId, ActorUserId,
            "ready.bin", null, new MemoryStream([1]));
        Assert.True(ready.Success, ready.Message);
        var pending = await Prepare([2]);

        Assert.Equal(1, await ResolveService<CardAttachmentService>().CleanupAtStartupAsync());

        var retained = await DbContextForAssert.CardAttachments.SingleAsync();
        Assert.Equal(ready.Data!.Id, retained.Id);
        Assert.Equal(AttachmentState.Ready, retained.State);
        Assert.Throws<FileNotFoundException>(() => ResolveService<IAttachmentStorageService>().OpenRead(pending.StorageKey));
    }

    [Fact]
    public async Task Startup_WhenDeletionFails_ShouldKeepPendingRecordAndRetryNextStartup()
    {
        var pending = await Prepare([1]);
        var storage = Assert.IsType<TestStorage>(ResolveService<IAttachmentStorageService>());
        storage.FailDelete = true;
        var files = ResolveService<CardAttachmentService>();

        Assert.Equal(0, await files.CleanupAtStartupAsync());
        Assert.Equal(pending.Id, (await DbContextForAssert.CardAttachments.SingleAsync()).Id);
        storage.FailDelete = false;
        Assert.Equal(1, await files.CleanupAtStartupAsync());
        Assert.Empty(await DbContextForAssert.CardAttachments.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deletion_WhenStorageFails_ShouldRetainAttachmentWithErrorUntilStartup(bool fromInventory)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var cardId = board.GetCard("Card").BoardCardId;
        var service = ResolveService<ICardAttachmentService>();
        var ready = await service.UploadAsync(board.BoardId, cardId, ActorUserId, "file.bin", null, new MemoryStream([1]));
        var storage = Assert.IsType<TestStorage>(ResolveService<IAttachmentStorageService>());
        storage.FailDelete = true;

        var result = fromInventory
            ? await service.DeleteFromBoardAsync(board.BoardId, ready.Data!.Id, ActorUserId)
            : await service.DeleteAsync(board.BoardId, cardId, ready.Data!.Id, ActorUserId);

        Assert.True(result.Success, result.Message);
        var retained = await DbContextForAssert.CardAttachments.SingleAsync();
        Assert.Equal(ready.Data.Id, retained.Id);
        Assert.Equal(AttachmentState.PendingDeletion, retained.State);
        Assert.Null(retained.CardId);
        Assert.Null(retained.ArchivedCardId);
        Assert.Contains("Simulated", retained.LastError);
        storage.FailDelete = false;
        Assert.Equal(1, await ResolveService<CardAttachmentService>().CleanupAtStartupAsync());
    }

    [Fact]
    public async Task Upload_AfterFailedUpload_ShouldAllowRetryWithSameFilename()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var cardId = board.GetCard("Card").BoardCardId;
        var service = ResolveService<ICardAttachmentService>();
        var failed = await service.UploadAsync(board.BoardId, cardId, ActorUserId, "file.bin", null, new MemoryStream(new byte[1025]));
        Assert.Equal(413, failed.StatusCode);

        var retried = await service.UploadAsync(board.BoardId, cardId, ActorUserId, "FILE.BIN", null, new MemoryStream([1]));

        Assert.True(retried.Success, retried.Message);
        var records = await DbContextForAssert.CardAttachments.ToListAsync();
        Assert.Single(records, x => x.State == AttachmentState.Ready);
        Assert.Null(Assert.Single(records, x => x.State == AttachmentState.PendingDeletion).CardId);
    }

    [Fact]
    public async Task Upload_AfterDeletionFails_ShouldAllowFilenameReuseAndKeepNewFileDuringRecovery()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var cardId = board.GetCard("Card").BoardCardId;
        var service = ResolveService<ICardAttachmentService>();
        var original = await service.UploadAsync(board.BoardId, cardId, ActorUserId, "file.bin", null, new MemoryStream([1]));
        var storage = Assert.IsType<TestStorage>(ResolveService<IAttachmentStorageService>());
        storage.FailDelete = true;
        Assert.True((await service.DeleteAsync(board.BoardId, cardId, original.Data!.Id, ActorUserId)).Success);

        var replacement = await service.UploadAsync(board.BoardId, cardId, ActorUserId, "FILE.BIN", null, new MemoryStream([2]));

        Assert.True(replacement.Success, replacement.Message);
        Assert.Single((await service.ListAsync(board.BoardId, cardId, false, ActorUserId)).Data!.Items);
        Assert.Equal(404, (await service.DownloadAsync(board.BoardId, original.Data.Id, ActorUserId)).StatusCode);
        storage.FailDelete = false;
        Assert.Equal(1, await ResolveService<CardAttachmentService>().CleanupAtStartupAsync());
        Assert.Equal(replacement.Data!.Id, (await DbContextForAssert.CardAttachments.SingleAsync()).Id);
        var download = await service.DownloadAsync(board.BoardId, replacement.Data.Id, ActorUserId);
        await using var content = download.Data!.Content;
        Assert.Equal(2, content.ReadByte());
    }

    [Fact]
    public async Task Publish_WhenTransactionRollsBack_ShouldLeaveSamePendingRecord()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var pending = await Prepare([1]);
        using (var scope = ResolveService<IDbContextScopeFactory>().CreateWithTransaction(System.Data.IsolationLevel.Serializable))
        {
            var record = ResolveService<CardAttachmentService>().Publish(pending);
            record.CardId = board.GetCard("Card").Id;
            // Deliberately do not commit.
        }
        var retained = await DbContextForAssert.CardAttachments.SingleAsync();
        Assert.Equal(pending.Id, retained.Id);
        Assert.Equal(AttachmentState.Pending, retained.State);
    }

    [Fact]
    public async Task ReadyAttachment_WithoutOwner_ShouldBeRejectedByDatabase()
    {
        DbContextForArrange.CardAttachments.Add(new EntityCardAttachment
        {
            State = AttachmentState.Ready,
            OriginalFileName = "file",
            ContentType = "application/octet-stream",
            StorageKey = Guid.NewGuid().ToString("N"),
            Sha256 = new string('a', 64)
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => DbContextForArrange.SaveChangesAsync());
    }

    [Fact]
    public async Task Startup_ShouldRecoverOnlyDatabaseTrackedFilesAndPackages()
    {
        var files = ResolveService<CardAttachmentService>();
        var packages = ResolveService<BoardPackageStorageService>();
        var package = await packages.CreateAsync();
        var path = package.Name;
        var record = await DbContextForArrange.TemporaryBoardPackages.AsNoTracking().SingleAsync();
        await package.DisposeAsync();
        // Recreate an interrupted operation: record first, then its abandoned file.
        record.Id = 0;
        DbContextForArrange.TemporaryBoardPackages.Add(record);
        await DbContextForArrange.SaveChangesAsync();
        await File.WriteAllBytesAsync(path, [1]);
        var storage = ResolveService<IAttachmentStorageService>();
        var unknownKey = Guid.NewGuid().ToString("N");
        await storage.Create(unknownKey).DisposeAsync();

        Assert.Equal(1, await packages.CleanupAtStartupAsync());
        Assert.Equal(0, await files.CleanupAtStartupAsync());

        Assert.False(File.Exists(path));
        Assert.Empty(await DbContextForAssert.TemporaryBoardPackages.ToListAsync());
        await using var unknown = storage.OpenRead(unknownKey);
        Assert.Equal(0, unknown.Length);
    }

    [Fact]
    public async Task PutThumbnail_WhenStagedWriteFails_ShouldLeaveNoPublishedKeyAndAllowRetry()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var cardId = board.GetCard("Card").BoardCardId;
        var service = ResolveService<ICardAttachmentService>();
        var uploaded = await service.UploadAsync(board.BoardId, cardId, ActorUserId,
            "image.png", "image/png", new MemoryStream(Png(3, 2)));
        Assert.True(uploaded.Success, uploaded.Message);
        Assert.NotNull(uploaded.Data);
        var storage = Assert.IsType<TestStorage>(ResolveService<IAttachmentStorageService>());
        storage.FailNextWrite = true;

        await Assert.ThrowsAsync<IOException>(() => service.PutThumbnailAsync(board.BoardId, uploaded.Data.Id,
            ActorUserId, "image/png", new MemoryStream(Png(2, 2))));

        Assert.Null((await DbContextForAssert.CardAttachments.AsNoTracking().SingleAsync()).ThumbnailStorageKey);
        Assert.Single(Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories));
        var retried = await service.PutThumbnailAsync(board.BoardId, uploaded.Data.Id,
            ActorUserId, "image/png", new MemoryStream(Png(2, 2)));
        Assert.True(retried.Success, retried.Message);
        Assert.NotNull((await DbContextForAssert.CardAttachments.AsNoTracking().SingleAsync()).ThumbnailStorageKey);
        Assert.Equal(2, Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories).Count());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PackageDisposal_ShouldRemoveFileAndTrackingRow(bool asynchronous)
    {
        var package = await ResolveService<BoardPackageStorageService>().CreateAsync();
        var path = package.Name;
        Assert.Single(await DbContextForAssert.TemporaryBoardPackages.AsNoTracking().ToListAsync());
        Assert.True(File.Exists(path));

        if (asynchronous) { await package.DisposeAsync(); }
        else { package.Dispose(); }

        Assert.False(File.Exists(path));
        Assert.Empty(await DbContextForAssert.TemporaryBoardPackages.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task PackageDisposal_WhenRecordRemovalFails_ShouldLeaveItForStartupWithoutFailingResponse()
    {
        var packages = ResolveService<BoardPackageStorageService>();
        var package = await packages.CreateAsync();
        await DbContextForArrange.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER reject_package_cleanup BEFORE DELETE ON TemporaryBoardPackages
            BEGIN SELECT RAISE(ABORT, 'Simulated cleanup failure'); END;
            """);

        await package.DisposeAsync();

        Assert.False(File.Exists(package.Name));
        Assert.Single(await DbContextForAssert.TemporaryBoardPackages.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData("../file.txt", "file.txt")]
    [InlineData("C:\\folder\\image.png", "image.png")]
    [InlineData("résumé.txt", "résumé.txt")]
    public void Filename_ShouldDiscardDirectoriesOnly(string input, string expected) =>
        Assert.Equal(expected, AttachmentFileMetadata.FileName(input));

    [Theory]
    [InlineData("bad\r\nname")]
    [InlineData("..")]
    [InlineData("")]
    public void Filename_ShouldRejectInvalidNames(string input) =>
        Assert.Throws<ArgumentException>(() => AttachmentFileMetadata.FileName(input));

    private Task<PreparedAttachmentFile> Prepare(byte[] content) =>
        ResolveService<CardAttachmentService>().PrepareAsync(new MemoryStream(content), "file.bin", null, ActorUserId);

    private static byte[] Png(int width, int height)
    {
        var bytes = new byte[24];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
        bytes[11] = 13;
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20, 4), height);
        return bytes;
    }

    private sealed class TestStorage(IAttachmentStorageService inner) : IAttachmentStorageService
    {
        public bool FailDelete { get; set; }
        public bool FailCreate { get; set; }
        public bool FailNextWrite { get; set; }
        public Action<string>? BeforeCreate { get; set; }
        public Stream Create(string storageKey)
        {
            BeforeCreate?.Invoke(storageKey);
            if (FailCreate) { throw new IOException("Simulated unavailable storage."); }
            var stream = inner.Create(storageKey);
            if (!FailNextWrite) { return stream; }
            FailNextWrite = false;
            return new PartialWriteFailureStream(stream);
        }
        public Stream OpenRead(string storageKey) => inner.OpenRead(storageKey);
        public void Delete(string storageKey)
        {
            if (FailDelete) { throw new IOException("Simulated unavailable storage."); }
            inner.Delete(storageKey);
        }
    }

    private sealed class PartialWriteFailureStream(Stream inner) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => true;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count)
        {
            inner.Write(buffer, offset, Math.Min(1, count));
            throw new IOException("Simulated interrupted thumbnail write.");
        }
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            await inner.WriteAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
            throw new IOException("Simulated interrupted thumbnail write.");
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { inner.Dispose(); }
            base.Dispose(disposing);
        }
        public override async ValueTask DisposeAsync() => await inner.DisposeAsync();
    }
}
