using System.IO.Compression;
using System.Text.Json;
using BoardOil.Abstractions.Attachment;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.Card;
using BoardOil.Contracts.Board;
using BoardOil.Services.Attachment;
using BoardOil.Services.Board;
using BoardOil.Services.Tests.Infrastructure;
using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class AttachmentPackageTests : TestBaseDb, IAsyncLifetime
{
    private const string LiveDescription = "![Live diagram](boardoil-attachment:Live.png)";
    private const string ArchivedDescription = "![Archived diagram](boardoil-attachment:Archived.png)";
    private static readonly byte[] ImageBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    private readonly AttachmentStorageOptions _options = new()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "boardoil-attachment-package-" + Guid.NewGuid().ToString("N")),
        MaxUploadByteLength = 1024
    };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    protected override void ConfigureTestServices(IServiceCollection services) => services.AddSingleton(_options);
    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (Directory.Exists(_options.RootPath)) { Directory.Delete(_options.RootPath, true); }
    }

    [Fact]
    public async Task RoundTrip_ShouldPreserveLiveAndArchivedMetadataAndBytesWithIndependentFiles()
    {
        var boardId = await ArrangeBoard();
        var exported = await ResolveService<IBoardExportService>().ExportBoardAsync(boardId, ActorUserId, "test");
        Assert.True(exported.Success, exported.Message);
        await using var package = exported.Data!.Content;

        var imported = await ResolveService<IBoardPackageImportService>().ImportBoardPackageAsync(new("Imported", package), ActorUserId);

        Assert.True(imported.Success, imported.Message);
        var originals = await DbContextForAssert.CardAttachments.OrderBy(x => x.Id).Take(2).ToListAsync();
        var copies = await DbContextForAssert.CardAttachments.OrderBy(x => x.Id).Skip(2).ToListAsync();
        Assert.Equal(2, copies.Count);
        Assert.Single(copies, x => x.ArchivedCardId.HasValue);
        Assert.Single(copies, x => x.CardId.HasValue);
        foreach (var original in originals)
        {
            var copy = Assert.Single(copies, x => x.OriginalFileName == original.OriginalFileName);
            Assert.NotEqual(original.Id, copy.Id);
            Assert.NotEqual(original.StorageKey, copy.StorageKey);
            Assert.Equal(original.CreatedAtUtc, copy.CreatedAtUtc);
            Assert.Equal(original.CreatedByUserId, copy.CreatedByUserId);
            Assert.Equal(original.ContentType, copy.ContentType);
            Assert.Equal(original.Sha256, copy.Sha256);
            var downloaded = await ResolveService<ICardAttachmentService>().DownloadAsync(imported.Data!.Id, copy.Id, ActorUserId);
            Assert.True(downloaded.Success, downloaded.Message);
            await using var content = downloaded.Data!.Content;
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer);
            Assert.Equal(ImageBytes, buffer.ToArray());
        }

        var importedBoard = imported.Data!;
        var importedLiveCard = Assert.Single(importedBoard.Columns.SelectMany(x => x.Cards), x => x.Title == "Live");
        Assert.Equal(LiveDescription, importedLiveCard.Description);
        await AssertImageContent(importedBoard.Id, importedLiveCard.Id, false, "Live.png");

        var importedArchivedCardId = await DbContextForAssert.ArchivedCards
            .Where(x => x.BoardId == importedBoard.Id)
            .Select(x => x.OriginalCardId)
            .SingleAsync();
        var importedArchivedCard = await ResolveService<ICardArchiveService>()
            .GetArchivedCardAsync(importedBoard.Id, importedArchivedCardId, ActorUserId);
        Assert.True(importedArchivedCard.Success, importedArchivedCard.Message);
        Assert.Equal(ArchivedDescription, importedArchivedCard.Data!.Card.Description);
        await AssertImageContent(importedBoard.Id, importedArchivedCardId, true, "Archived.png");

        // Import is complete; only the still-open export should remain tracked.
        Assert.Single(await DbContextForAssert.TemporaryBoardPackages.AsNoTracking().ToListAsync());
        await package.DisposeAsync();
        Assert.Empty(await DbContextForAssert.TemporaryBoardPackages.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task RoundTrip_ShouldOmitDerivedThumbnailAndAllowImportedBackfill()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Image").Build();
        var card = board.GetCard("Image");
        var service = ResolveService<ICardAttachmentService>();
        var uploaded = await service.UploadAsync(board.BoardId, card.BoardCardId, ActorUserId,
            "image.png", "image/png", new MemoryStream(ImageBytes), new MemoryStream(ImageBytes), "image/png");
        Assert.True(uploaded.Success, uploaded.Message);
        var exported = await ResolveService<IBoardExportService>().ExportBoardAsync(board.BoardId, ActorUserId, "test");
        Assert.True(exported.Success, exported.Message);
        await using var package = exported.Data!.Content;

        var imported = await ResolveService<IBoardPackageImportService>()
            .ImportBoardPackageAsync(new("Imported thumbnails", package), ActorUserId);

        Assert.True(imported.Success, imported.Message);
        var records = await DbContextForAssert.CardAttachments.OrderBy(x => x.Id).ToListAsync();
        Assert.NotNull(records[0].ThumbnailStorageKey);
        Assert.Null(records[1].ThumbnailStorageKey);
        Assert.True((await service.PutThumbnailAsync(imported.Data.Id, records[1].Id, ActorUserId,
            "image/png", new MemoryStream(ImageBytes))).Success);
        Assert.True((await service.ViewThumbnailAsync(imported.Data.Id, records[1].Id, ActorUserId)).Success);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedExport_ShouldRemoveTemporaryFileAndTrackingRowAfterClosingSnapshot(bool corruptFile)
    {
        var boardId = await ArrangeBoard();
        var key = await DbContextForArrange.CardAttachments.OrderBy(x => x.Id).Select(x => x.StorageKey).FirstAsync();
        var storage = ResolveService<IAttachmentStorageService>();
        storage.Delete(key);
        if (corruptFile)
        {
            await using var output = storage.Create(key);
            await output.WriteAsync(new byte[] { 1, 1, 1, 1 });
        }

        var result = await ResolveService<IBoardExportService>().ExportBoardAsync(boardId, ActorUserId, "test");

        Assert.False(result.Success);
        Assert.Empty(await DbContextForAssert.TemporaryBoardPackages.AsNoTracking().ToListAsync());
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_options.RootPath, "packages")));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("checksum")]
    [InlineData("owner")]
    [InlineData("duplicate-metadata")]
    [InlineData("duplicate-name-live")]
    [InlineData("duplicate-name-archived")]
    [InlineData("duplicate-entry")]
    [InlineData("unsafe-path")]
    [InlineData("length")]
    [InlineData("file-limit")]
    public async Task InvalidPackage_ShouldNotPublishPartialBoardAndShouldRecoverPreparedFiles(string fault)
    {
        var boardId = await ArrangeBoard();
        var exported = await ResolveService<IBoardExportService>().ExportBoardAsync(boardId, ActorUserId, "test");
        Assert.True(exported.Success, exported.Message);
        await using var exportedContent = exported.Data!.Content;
        using var package = new MemoryStream();
        await exportedContent.CopyToAsync(package);
        package.Position = 0;
        using (var archive = new ZipArchive(package, ZipArchiveMode.Update, leaveOpen: true))
        {
            var metadataEntry = archive.GetEntry(BoardPackageContract.AttachmentsEntryPath)!;
            BoardPackageAttachmentsDto metadata;
            using (var input = metadataEntry.Open()) { metadata = JsonSerializer.Deserialize<BoardPackageAttachmentsDto>(input, JsonOptions)!; }
            var last = metadata.Items[^1];
            switch (fault)
            {
                case "missing": archive.GetEntry(last.Path)!.Delete(); break;
                case "duplicate-entry": archive.CreateEntry(last.Path); break;
                case "unsafe-path": archive.CreateEntry("../escape"); break;
                default:
                    metadataEntry.Delete();
                    var changed = fault switch
                    {
                        "checksum" => last with { Sha256 = new string('0', 64) },
                        "owner" => last with { CardId = 99999 },
                        "length" => last with { ByteLength = 123 },
                        "file-limit" => last with { ByteLength = _options.MaxUploadByteLength + 1 },
                        _ => last
                    };
                    var items = metadata.Items.ToList();
                    items[^1] = changed;
                    if (fault == "duplicate-metadata") { items.Add(last); }
                    if (fault is "duplicate-name-live" or "duplicate-name-archived")
                    {
                        var source = items.Single(x => x.Archived == (fault == "duplicate-name-archived"));
                        var copy = source with { Path = "files/" + Guid.NewGuid().ToString("N"), OriginalFileName = source.OriginalFileName.ToUpperInvariant() };
                        items.Add(copy);
                        using var file = archive.CreateEntry(copy.Path).Open();
                        file.Write(ImageBytes);
                    }
                    if (fault == "file-limit")
                    {
                        archive.GetEntry(last.Path)!.Delete();
                        using var file = archive.CreateEntry(last.Path).Open();
                        file.Write(new byte[_options.MaxUploadByteLength + 1]);
                    }
                    using (var output = archive.CreateEntry(BoardPackageContract.AttachmentsEntryPath).Open())
                    {
                        JsonSerializer.Serialize(output, new BoardPackageAttachmentsDto(items), JsonOptions);
                    }
                    break;
            }
        }
        package.Position = 0;

        var result = await ResolveService<IBoardPackageImportService>().ImportBoardPackageAsync(new("Invalid", package), ActorUserId);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Single(await DbContextForAssert.TemporaryBoardPackages.AsNoTracking().ToListAsync());
        Assert.Single(await DbContextForAssert.Boards.ToListAsync());
        Assert.Equal(2, await DbContextForAssert.CardAttachments.CountAsync(x => x.State == AttachmentState.Ready));
        await exportedContent.DisposeAsync();
        Assert.Empty(await DbContextForAssert.TemporaryBoardPackages.AsNoTracking().ToListAsync());
        await ResolveService<CardAttachmentService>().CleanupAtStartupAsync();
        Assert.Equal(2, await DbContextForAssert.CardAttachments.CountAsync());
        Assert.Equal(2, Directory.EnumerateFiles(_options.RootPath, "*", SearchOption.AllDirectories).Count(path => !path.EndsWith(".tmp")));
    }

    [Fact]
    public async Task CancelledImport_ShouldRemoveItsTemporaryPackageAndNotCreateBoard()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ResolveService<IBoardPackageImportService>()
            .ImportBoardPackageAsync(new(null, new byte[] { 1 }), ActorUserId, cancellation.Token));
        Assert.Empty(await DbContextForAssert.Boards.ToListAsync());
        Assert.False(Directory.Exists(_options.RootPath));
    }

    private async Task<int> ArrangeBoard()
    {
        var board = CreateBoard()
            .AddColumn("Todo")
            .AddCard("Live", LiveDescription)
            .AddCard("Archived", ArchivedDescription)
            .Build();
        var service = ResolveService<ICardAttachmentService>();
        foreach (var name in new[] { "Live", "Archived" })
        {
            var uploaded = await service.UploadAsync(board.BoardId, board.GetCard(name).BoardCardId, ActorUserId,
                name + ".png", "image/png", new MemoryStream(ImageBytes));
            Assert.True(uploaded.Success, uploaded.Message);
        }
        Assert.True((await ResolveService<ICardArchiveService>().ArchiveCardAsync(board.BoardId, board.GetCard("Archived").BoardCardId, ActorUserId)).Success);
        return board.BoardId;
    }

    private async Task AssertImageContent(int boardId, int cardId, bool archived, string fileName)
    {
        var result = await ResolveService<ICardAttachmentService>()
            .ViewImageAsync(boardId, cardId, archived, fileName, ActorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal("image/png", result.Data!.ContentType);
        Assert.Equal(1, result.Data.Width);
        Assert.Equal(1, result.Data.Height);
        await using var content = result.Data.Content;
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer);
        Assert.Equal(ImageBytes, buffer.ToArray());
    }
}
