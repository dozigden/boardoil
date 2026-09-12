using System.Security.Cryptography;
using System.Text;
using BoardOil.Abstractions.Attachment;
using BoardOil.Abstractions.Card;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Attachment;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Ef;
using BoardOil.Services.Attachment;
using BoardOil.Services.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class AttachmentTransferServiceTests : TestBaseDb, IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boardoil-transfer-tests-" + Guid.NewGuid().ToString("N"));
    private readonly TestClock _clock = new();
    private readonly TestCredentials _credentials = new();
    private readonly TestAuditRepositoryState _auditRepository = new();
    private static readonly AttachmentTransferCredential Credential = new(1, null, null);
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton(new AttachmentStorageOptions { RootPath = _root });
        services.AddSingleton<TimeProvider>(_clock);
        services.AddSingleton<IAttachmentTransferCredentialValidator>(_credentials);
        services.AddSingleton(_auditRepository);
        services.AddScoped<IAttachmentTransferAuditRepository, TestAuditRepository>();
        services.AddLogging();
    }

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (Directory.Exists(_root)) { Directory.Delete(_root, true); }
    }

    [Fact]
    public async Task Issue_ShouldPersistOnlyHashAndExactOwnerWithTwoMinuteExpiry()
    {
        var (boardId, cardId, attachmentId) = await ArrangeAttachment();

        var result = await ResolveService<IAttachmentTransferService>().IssueDownloadAsync(boardId, attachmentId, ActorUserId, Credential);

        Assert.True(result.Success, result.Message);
        var ticket = result.Data!;
        var row = await DbContextForAssert.AttachmentDownloadTickets.SingleAsync();
        Assert.Equal(64, ticket.Secret.Length);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ticket.Secret))), row.SecretHash);
        Assert.DoesNotContain(ticket.Secret, ticket.ToString());
        Assert.Equal(_clock.Now.AddMinutes(2).UtcDateTime, row.ExpiresAtUtc);
        Assert.Equal(attachmentId, row.AttachmentId);
        Assert.Equal(cardId, row.CardNumber);
        Assert.Equal(boardId, row.BoardId);
        Assert.Equal(ActorUserId, row.ActorUserId);
        Assert.Equal(1, row.PersonalAccessTokenId);
        var audit = await DbContextForAssert.AttachmentTransferAudits.SingleAsync();
        Assert.Equal(row.Id, audit.TicketId);
        Assert.Equal(ActorUserId, audit.ActorUserId);
        Assert.Equal(boardId, audit.BoardId);
        Assert.Equal(attachmentId, audit.AttachmentId);
        Assert.Equal(AttachmentTransferCredentialType.PersonalAccessToken, audit.CredentialType);
        Assert.Equal(AttachmentTransferAuditOutcome.Issued, audit.Outcome);
        Assert.Equal(_clock.Now.UtcDateTime, audit.OccurredAtUtc);
        Assert.Equal([MachinePatScopes.McpRead], _credentials.RequiredScopes);
    }

    [Fact]
    public async Task Issue_ShouldCapExpiryAtCredentialExpiry()
    {
        var (boardId, _, attachmentId) = await ArrangeAttachment();
        _credentials.Expires = _clock.Now.AddMinutes(1).UtcDateTime;

        var result = await ResolveService<IAttachmentTransferService>().IssueDownloadAsync(boardId, attachmentId, ActorUserId, Credential);

        Assert.Equal(_credentials.Expires, result.Data!.ExpiresAtUtc);
    }

    [Fact]
    public async Task Issue_WhenCredentialInvalid_ShouldNotCreateRecord()
    {
        var (boardId, _, attachmentId) = await ArrangeAttachment();
        _credentials.Valid = false;

        var result = await ResolveService<IAttachmentTransferService>().IssueDownloadAsync(boardId, attachmentId, ActorUserId, Credential);

        Assert.Equal(401, result.StatusCode);
        Assert.Empty(await DbContextForAssert.AttachmentDownloadTickets.ToListAsync());
        Assert.Empty(await DbContextForAssert.AttachmentTransferAudits.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Download_ShouldAllowRepeatedReadsOfOriginalLiveOrArchivedBytes(bool archived)
    {
        var (boardId, cardId, attachmentId) = await ArrangeAttachment();
        if (archived) { Assert.True((await ResolveService<ICardArchiveService>().ArchiveCardAsync(boardId, cardId, ActorUserId)).Success); }
        var ticket = await Issue(boardId, attachmentId);

        var results = new List<byte[]>();
        for (var i = 0; i < 2; i++)
        {
            var result = await ResolveService<IAttachmentTransferService>().DownloadAsync(ticket.Id, ticket.Secret);
            Assert.True(result.Success, result.Message);
            await using var stream = result.Data!.Content;
            using var bytes = new MemoryStream();
            await stream.CopyToAsync(bytes);
            results.Add(bytes.ToArray());
        }

        Assert.All(results, bytes => Assert.Equal(new byte[] { 0, 255, 13, 10, 42 }, bytes));
        Assert.Single(await DbContextForAssert.AttachmentDownloadTickets.ToListAsync());
        var audits = await DbContextForAssert.AttachmentTransferAudits.OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(
            [AttachmentTransferAuditOutcome.Issued, AttachmentTransferAuditOutcome.DownloadAdmitted, AttachmentTransferAuditOutcome.DownloadAdmitted],
            audits.Select(x => x.Outcome));
        Assert.All(audits, audit => Assert.Equal(ticket.Id, audit.TicketId));
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("secret")]
    [InlineData("credential")]
    [InlineData("membership")]
    [InlineData("deleted")]
    [InlineData("archived")]
    [InlineData("restored")]
    [InlineData("archive-round-trip")]
    [InlineData("board")]
    [InlineData("board-round-trip")]
    public async Task Download_WhenTicketOrAccessChanges_ShouldReject(string change)
    {
        var (boardId, cardId, attachmentId) = await ArrangeAttachment();
        var archiveService = ResolveService<ICardArchiveService>();
        if (change == "restored") { Assert.True((await archiveService.ArchiveCardAsync(boardId, cardId, ActorUserId)).Success); }
        var ticket = await Issue(boardId, attachmentId);
        switch (change)
        {
            case "expired": _clock.Now = _clock.Now.AddMinutes(2); break;
            case "secret": ticket = ticket with { Secret = new string('z', 64) }; break;
            case "credential": _credentials.Valid = false; break;
            case "membership":
                DbContextForArrange.BoardMembers.Remove(await DbContextForArrange.BoardMembers.SingleAsync(x => x.BoardId == boardId));
                await DbContextForArrange.SaveChangesAsync();
                break;
            case "deleted":
                Assert.True((await ResolveService<ICardAttachmentService>().DeleteAsync(boardId, cardId, attachmentId, ActorUserId)).Success);
                break;
            case "archived":
                Assert.True((await archiveService.ArchiveCardAsync(boardId, cardId, ActorUserId)).Success);
                break;
            case "restored":
                Assert.True((await archiveService.UnarchiveCardAsync(boardId, cardId, ActorUserId)).Success);
                break;
            case "archive-round-trip":
                Assert.True((await archiveService.ArchiveCardAsync(boardId, cardId, ActorUserId)).Success);
                Assert.True((await archiveService.UnarchiveCardAsync(boardId, cardId, ActorUserId)).Success);
                break;
            case "board":
            case "board-round-trip":
                var destination = CreateBoard("Destination").AddColumn("Todo").Build();
                var card = await DbContextForArrange.Cards.SingleAsync(x => x.BoardId == boardId);
                var originalColumn = card.BoardColumnId;
                var cardService = ResolveService<ICardService>();
                var moved = await cardService.TransferCardAsync(boardId, cardId,
                    new TransferCardRequest(destination.BoardId, destination.GetColumn("Todo").Id, CardTransferPolicies.DestinationDefaults), ActorUserId);
                Assert.True(moved.Success, moved.Message);
                if (change == "board-round-trip")
                {
                    var newNumber = await DbContextForAssert.Cards.Where(x => x.Id == card.Id).Select(x => x.BoardCardId).SingleAsync();
                    var returned = await cardService.TransferCardAsync(destination.BoardId, newNumber,
                        new TransferCardRequest(boardId, originalColumn, CardTransferPolicies.DestinationDefaults), ActorUserId);
                    Assert.True(returned.Success, returned.Message);
                }
                break;
        }

        var result = await ResolveService<IAttachmentTransferService>().DownloadAsync(ticket.Id, ticket.Secret);

        Assert.False(result.Success);
        Assert.Contains(result.StatusCode, new[] { 401, 403 });
        var outcomes = await DbContextForAssert.AttachmentTransferAudits.OrderBy(x => x.Id).Select(x => x.Outcome).ToListAsync();
        var expected = change switch
        {
            "expired" => new[] { AttachmentTransferAuditOutcome.Issued, AttachmentTransferAuditOutcome.Expired },
            "secret" or "deleted" => [AttachmentTransferAuditOutcome.Issued],
            "credential" => [AttachmentTransferAuditOutcome.Issued, AttachmentTransferAuditOutcome.CredentialInvalid],
            "membership" => [AttachmentTransferAuditOutcome.Issued, AttachmentTransferAuditOutcome.AccessDenied],
            _ => new[] { AttachmentTransferAuditOutcome.Issued, AttachmentTransferAuditOutcome.OwnerChanged }
        };
        Assert.Equal(expected, outcomes);
    }

    [Fact]
    public async Task AdmittedDownload_ShouldFinishAfterExpiry()
    {
        var (boardId, _, attachmentId) = await ArrangeAttachment();
        var ticket = await Issue(boardId, attachmentId);
        var admitted = await ResolveService<IAttachmentTransferService>().DownloadAsync(ticket.Id, ticket.Secret);
        _clock.Now = _clock.Now.AddMinutes(3);

        await using var stream = admitted.Data!.Content;
        using var bytes = new MemoryStream();
        await stream.CopyToAsync(bytes);

        Assert.Equal(new byte[] { 0, 255, 13, 10, 42 }, bytes.ToArray());
    }

    [Fact]
    public async Task Cleanup_ShouldRemoveOnlyExpiredTicketsWithoutRemovingAttachments()
    {
        var (boardId, _, attachmentId) = await ArrangeAttachment();
        await Issue(boardId, attachmentId);
        _clock.Now = _clock.Now.AddMinutes(2);
        var retained = await Issue(boardId, attachmentId);

        var count = await ResolveService<AttachmentTransferService>().CleanupAtStartupAsync();

        Assert.Equal(1, count);
        Assert.Equal(retained.Id, (await DbContextForAssert.AttachmentDownloadTickets.SingleAsync()).Id);
        Assert.Equal(attachmentId, (await DbContextForAssert.CardAttachments.SingleAsync()).Id);
        Assert.Equal(2, await DbContextForAssert.AttachmentTransferAudits.CountAsync());
    }

    [Fact]
    public async Task Cleanup_ShouldRemoveAuditsOlderThanRetentionPeriod()
    {
        var (boardId, _, attachmentId) = await ArrangeAttachment();
        var expired = await Issue(boardId, attachmentId);
        _clock.Now = _clock.Now.AddDays(AttachmentTransferAuditRetention.Days).AddTicks(1);
        var retained = await Issue(boardId, attachmentId);

        var count = await ResolveService<AttachmentTransferService>().CleanupAtStartupAsync();

        Assert.Equal(1, count);
        Assert.Equal(retained.Id, (await DbContextForAssert.AttachmentDownloadTickets.SingleAsync()).Id);
        var audit = await DbContextForAssert.AttachmentTransferAudits.SingleAsync();
        Assert.Equal(retained.Id, audit.TicketId);
        Assert.NotEqual(expired.Id, audit.TicketId);
    }

    [Fact]
    public async Task Download_WhenTicketIdIsUnknown_ShouldNotCreateAuditRecord()
    {
        var result = await ResolveService<IAttachmentTransferService>().DownloadAsync(123456, new string('a', 64));

        Assert.Equal(401, result.StatusCode);
        Assert.Empty(await DbContextForAssert.AttachmentTransferAudits.ToListAsync());
    }

    [Fact]
    public async Task IssueUpload_ShouldReserveNameAndPersistOnlySecretHash()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");

        var result = await ResolveService<IAttachmentTransferService>().IssueUploadAsync(
            board.BoardId, card.BoardCardId, ActorUserId, "folder/Original.bin", "APPLICATION/OCTET-STREAM", 5, Credential);

        Assert.True(result.Success, result.Message);
        var ticket = result.Data!;
        var row = await DbContextForAssert.AttachmentUploadTickets.Include(x => x.Attachment).SingleAsync();
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ticket.Secret))), row.SecretHash);
        Assert.DoesNotContain(ticket.Secret, ticket.ToString());
        Assert.Equal("Original.bin", ticket.OriginalFileName);
        Assert.Equal("Original.bin", row.OriginalFileName);
        Assert.Equal("application/octet-stream", row.ContentType);
        Assert.Equal(5, row.DeclaredByteLength);
        Assert.Equal(_clock.Now.AddMinutes(2).UtcDateTime, row.ExpiresAtUtc);
        Assert.Equal(AttachmentUploadTicketState.Issued, row.State);
        Assert.Equal(AttachmentState.Pending, row.Attachment.State);
        Assert.Equal(card.Id, row.CardId);
        var audit = await DbContextForAssert.AttachmentTransferAudits.SingleAsync();
        Assert.Equal(AttachmentTransferOperation.Upload, audit.Operation);
        Assert.Equal(AttachmentTransferAuditOutcome.Issued, audit.Outcome);
        Assert.Equal([MachinePatScopes.McpWrite], _credentials.RequiredScopes);
    }

    [Fact]
    public async Task IssueUpload_WhenIssuedAuditFails_ShouldRollBackTicketAndNameReservation()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        _auditRepository.OutcomeToThrow = AttachmentTransferAuditOutcome.Issued;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ResolveService<IAttachmentTransferService>().IssueUploadAsync(
                board.BoardId, card.BoardCardId, ActorUserId, "atomic.bin", null, 3, Credential));

        Assert.Empty(await DbContextForAssert.AttachmentUploadTickets.ToListAsync());
        Assert.Empty(await DbContextForAssert.CardAttachments.ToListAsync());
        Assert.Empty(await DbContextForAssert.AttachmentTransferAudits.ToListAsync());
    }

    [Fact]
    public async Task Upload_ShouldPublishExactBytesAndReturnCompletedResultOnRetryWithoutReadingReplacement()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        var ticket = await IssueUpload(board.BoardId, card.BoardCardId, "original.bin", 5);

        var uploaded = await ResolveService<IAttachmentTransferService>().UploadAsync(ticket.Id, ticket.Secret,
            ticket.ContentType, ticket.ByteLength, new MemoryStream([0, 255, 13, 10, 42]));
        var replacement = new ThrowOnReadStream();
        var repeated = await ResolveService<IAttachmentTransferService>().UploadAsync(ticket.Id, ticket.Secret,
            ticket.ContentType, ticket.ByteLength, replacement);

        Assert.True(uploaded.Success, uploaded.Message);
        Assert.True(repeated.Success, repeated.Message);
        Assert.Equal(uploaded.Data!.Id, repeated.Data!.Id);
        var row = await DbContextForAssert.CardAttachments.SingleAsync();
        Assert.Equal(AttachmentState.Ready, row.State);
        Assert.Equal(5, row.ByteLength);
        Assert.Equal("fab77ece7655adcad083c68aaa9b09bba131e5a90c443b9398ec7994591178c7", row.Sha256);
        var download = await ResolveService<ICardAttachmentService>().DownloadAsync(board.BoardId, row.Id, ActorUserId);
        await using var stream = download.Data!.Content;
        using var bytes = new MemoryStream();
        await stream.CopyToAsync(bytes);
        Assert.Equal(new byte[] { 0, 255, 13, 10, 42 }, bytes.ToArray());
        Assert.Equal(
            [AttachmentTransferAuditOutcome.Issued, AttachmentTransferAuditOutcome.UploadClaimed,
                AttachmentTransferAuditOutcome.UploadCompleted, AttachmentTransferAuditOutcome.UploadCompletedReturned],
            await DbContextForAssert.AttachmentTransferAudits.OrderBy(x => x.Id).Select(x => x.Outcome).ToListAsync());
        Assert.All(_credentials.RequiredScopes, scope => Assert.Equal(MachinePatScopes.McpWrite, scope));
    }

    [Fact]
    public async Task Upload_WhenCompletedAuditFails_ShouldStillComplete()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        var ticket = await IssueUpload(board.BoardId, card.BoardCardId, "completed.bin", 3);
        _auditRepository.OutcomeToThrow = AttachmentTransferAuditOutcome.UploadCompleted;

        var result = await ResolveService<IAttachmentTransferService>().UploadAsync(ticket.Id, ticket.Secret,
            ticket.ContentType, ticket.ByteLength, new MemoryStream([1, 2, 3]));

        Assert.True(result.Success, result.Message);
        var completed = await DbContextForAssert.AttachmentUploadTickets.Include(x => x.Attachment)
            .SingleAsync(x => x.Id == ticket.Id);
        Assert.Equal(AttachmentUploadTicketState.Completed, completed.State);
        Assert.Equal(AttachmentState.Ready, completed.Attachment.State);
        Assert.DoesNotContain(await DbContextForAssert.AttachmentTransferAudits.ToListAsync(),
            x => x.Outcome == AttachmentTransferAuditOutcome.UploadCompleted);
    }

    [Fact]
    public async Task Upload_WhenStreamLengthDiffers_ShouldFailTicketAndReleaseNameForNewTicket()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        var ticket = await IssueUpload(board.BoardId, card.BoardCardId, "retry.bin", 5);

        var result = await ResolveService<IAttachmentTransferService>().UploadAsync(ticket.Id, ticket.Secret,
            ticket.ContentType, ticket.ByteLength, new MemoryStream([1, 2, 3]));
        var replacement = await IssueUpload(board.BoardId, card.BoardCardId, "RETRY.BIN", 3);

        Assert.Equal(400, result.StatusCode);
        var failed = await DbContextForAssert.AttachmentUploadTickets.SingleAsync(x => x.Id == ticket.Id);
        Assert.Equal(AttachmentUploadTicketState.Failed, failed.State);
        Assert.NotNull(failed.FailedAtUtc);
        Assert.Equal(AttachmentState.PendingDeletion,
            await DbContextForAssert.CardAttachments.Where(x => x.Id == failed.AttachmentId).Select(x => x.State).SingleAsync());
        Assert.NotEqual(ticket.Id, replacement.Id);
    }

    [Fact]
    public async Task Upload_WhenFailedAuditFails_ShouldStillFailTicketAndReleaseName()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        var ticket = await IssueUpload(board.BoardId, card.BoardCardId, "failed.bin", 3);
        _auditRepository.OutcomeToThrow = AttachmentTransferAuditOutcome.UploadFailed;

        var result = await ResolveService<IAttachmentTransferService>().UploadAsync(ticket.Id, ticket.Secret,
            ticket.ContentType, ticket.ByteLength, new MemoryStream([1, 2]));

        Assert.Equal(400, result.StatusCode);
        var failed = await DbContextForAssert.AttachmentUploadTickets.Include(x => x.Attachment)
            .SingleAsync(x => x.Id == ticket.Id);
        Assert.Equal(AttachmentUploadTicketState.Failed, failed.State);
        Assert.Equal(AttachmentState.PendingDeletion, failed.Attachment.State);
        Assert.Null(failed.Attachment.CardId);
    }

    [Fact]
    public async Task Upload_WhenRequestHeadersDiffer_ShouldLeaveTicketAvailableForCorrectAttempt()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        var ticket = await IssueUpload(board.BoardId, card.BoardCardId, "headers.bin", 3);

        var rejected = await ResolveService<IAttachmentTransferService>().UploadAsync(ticket.Id, ticket.Secret,
            "text/plain", ticket.ByteLength, new ThrowOnReadStream());
        var accepted = await ResolveService<IAttachmentTransferService>().UploadAsync(ticket.Id, ticket.Secret,
            ticket.ContentType, ticket.ByteLength, new MemoryStream([1, 2, 3]));

        Assert.Equal(400, rejected.StatusCode);
        Assert.True(accepted.Success, accepted.Message);
        Assert.Equal(AttachmentUploadTicketState.Completed,
            await DbContextForAssert.AttachmentUploadTickets.Where(x => x.Id == ticket.Id).Select(x => x.State).SingleAsync());
    }

    [Theory]
    [InlineData("expired", AttachmentTransferAuditOutcome.Expired)]
    [InlineData("credential", AttachmentTransferAuditOutcome.CredentialInvalid)]
    [InlineData("access", AttachmentTransferAuditOutcome.AccessDenied)]
    [InlineData("owner", AttachmentTransferAuditOutcome.OwnerChanged)]
    public async Task Upload_WhenIssuedTicketBecomesUnusable_ShouldFailTicketAndReleaseName(
        string change, AttachmentTransferAuditOutcome expectedOutcome)
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").AddCard("Other").Build();
        var card = board.GetCard("Card");
        var ticket = await IssueUpload(board.BoardId, card.BoardCardId, "released.bin", 3);
        switch (change)
        {
            case "expired":
                _clock.Now = _clock.Now.AddMinutes(2);
                break;
            case "credential":
                _credentials.Valid = false;
                break;
            case "access":
                DbContextForArrange.BoardMembers.Remove(
                    await DbContextForArrange.BoardMembers.SingleAsync(x => x.BoardId == board.BoardId));
                await DbContextForArrange.SaveChangesAsync();
                break;
            case "owner":
                var attachment = await DbContextForArrange.CardAttachments.SingleAsync();
                attachment.CardId = board.GetCard("Other").Id;
                await DbContextForArrange.SaveChangesAsync();
                break;
        }

        var result = await ResolveService<IAttachmentTransferService>().UploadAsync(ticket.Id, ticket.Secret,
            ticket.ContentType, ticket.ByteLength, new ThrowOnReadStream());

        Assert.False(result.Success);
        var failed = await DbContextForAssert.AttachmentUploadTickets.Include(x => x.Attachment)
            .SingleAsync(x => x.Id == ticket.Id);
        Assert.Equal(AttachmentUploadTicketState.Failed, failed.State);
        Assert.NotNull(failed.FailedAtUtc);
        Assert.Equal(AttachmentState.PendingDeletion, failed.Attachment.State);
        Assert.Null(failed.Attachment.CardId);
        Assert.Contains(await DbContextForAssert.AttachmentTransferAudits.ToListAsync(),
            x => x.TicketId == ticket.Id && x.Outcome == expectedOutcome);
    }

    [Fact]
    public async Task Upload_WhenTerminalAuditFails_ShouldStillFailTicketAndReleaseName()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        var ticket = await IssueUpload(board.BoardId, card.BoardCardId, "terminal.bin", 3);
        _clock.Now = _clock.Now.AddMinutes(2);
        _auditRepository.OutcomeToThrow = AttachmentTransferAuditOutcome.Expired;

        var result = await ResolveService<IAttachmentTransferService>().UploadAsync(ticket.Id, ticket.Secret,
            ticket.ContentType, ticket.ByteLength, new ThrowOnReadStream());

        Assert.Equal(401, result.StatusCode);
        var failed = await DbContextForAssert.AttachmentUploadTickets.Include(x => x.Attachment)
            .SingleAsync(x => x.Id == ticket.Id);
        Assert.Equal(AttachmentUploadTicketState.Failed, failed.State);
        Assert.Equal(AttachmentState.PendingDeletion, failed.Attachment.State);
        Assert.Null(failed.Attachment.CardId);
    }

    [Fact]
    public async Task Upload_WhenRequestsOverlap_ShouldAllowOnlyTheClaimedRequestToReadBytes()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        var ticket = await IssueUpload(board.BoardId, card.BoardCardId, "concurrent.bin", 3);
        var blocking = new BlockingReadStream([1, 2, 3]);

        var firstTask = ResolveService<IAttachmentTransferService>().UploadAsync(ticket.Id, ticket.Secret,
            ticket.ContentType, ticket.ByteLength, blocking);
        await blocking.ReadStarted;
        var second = await ResolveService<IAttachmentTransferService>().UploadAsync(ticket.Id, ticket.Secret,
            ticket.ContentType, ticket.ByteLength, new ThrowOnReadStream());
        blocking.Release();
        var first = await firstTask;

        Assert.Equal(409, second.StatusCode);
        Assert.True(first.Success, first.Message);
        Assert.Single(await DbContextForAssert.CardAttachments.Where(x => x.State == AttachmentState.Ready).ToListAsync());
    }

    [Fact]
    public async Task Upload_WhenClaimedAuditFails_ShouldRollBackClaimWithoutReadingBytes()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        var ticket = await IssueUpload(board.BoardId, card.BoardCardId, "atomic.bin", 3);
        _auditRepository.OutcomeToThrow = AttachmentTransferAuditOutcome.UploadClaimed;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ResolveService<IAttachmentTransferService>().UploadAsync(ticket.Id, ticket.Secret,
                ticket.ContentType, ticket.ByteLength, new ThrowOnReadStream()));

        var retained = await DbContextForAssert.AttachmentUploadTickets.Include(x => x.Attachment)
            .SingleAsync(x => x.Id == ticket.Id);
        Assert.Equal(AttachmentUploadTicketState.Issued, retained.State);
        Assert.Equal(AttachmentState.Pending, retained.Attachment.State);
        Assert.Equal([AttachmentTransferAuditOutcome.Issued],
            await DbContextForAssert.AttachmentTransferAudits.Select(x => x.Outcome).ToListAsync());
    }

    [Fact]
    public async Task Upload_WhenCredentialIsRevokedDuringStreaming_ShouldNotPublishAttachment()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        var ticket = await IssueUpload(board.BoardId, card.BoardCardId, "revoked.bin", 3);
        var stream = new ActionAfterFirstReadStream([1, 2, 3], () => _credentials.Valid = false);

        var result = await ResolveService<IAttachmentTransferService>().UploadAsync(ticket.Id, ticket.Secret,
            ticket.ContentType, ticket.ByteLength, stream);

        Assert.Equal(401, result.StatusCode);
        Assert.Empty(await DbContextForAssert.CardAttachments.Where(x => x.State == AttachmentState.Ready).ToListAsync());
        Assert.Equal(AttachmentUploadTicketState.Failed,
            await DbContextForAssert.AttachmentUploadTickets.Where(x => x.Id == ticket.Id).Select(x => x.State).SingleAsync());
        Assert.Contains(await DbContextForAssert.AttachmentTransferAudits.ToListAsync(),
            x => x.Outcome == AttachmentTransferAuditOutcome.CredentialInvalid);
    }

    [Fact]
    public async Task IssueUpload_WhenDeclaredLengthExceedsLimit_ShouldCreateNothing()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");

        var result = await ResolveService<IAttachmentTransferService>().IssueUploadAsync(
            board.BoardId, card.BoardCardId, ActorUserId, "large.bin", null, 10 * 1024 * 1024 + 1, Credential);

        Assert.Equal(413, result.StatusCode);
        Assert.Empty(await DbContextForAssert.AttachmentUploadTickets.ToListAsync());
        Assert.Empty(await DbContextForAssert.CardAttachments.ToListAsync());
    }

    [Fact]
    public async Task Cleanup_ShouldRecoverIncompleteUploadAndItsStagedFile()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        var ticket = await IssueUpload(board.BoardId, card.BoardCardId, "abandoned.bin", 3);
        var storageKey = await DbContextForAssert.AttachmentUploadTickets.Where(x => x.Id == ticket.Id)
            .Select(x => x.Attachment.StorageKey).SingleAsync();
        var path = Path.Combine(_root, storageKey[..2], storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, [1, 2]);

        var recovered = await ResolveService<AttachmentTransferService>().CleanupAtStartupAsync();
        var deleted = await ResolveService<CardAttachmentService>().CleanupAtStartupAsync();

        Assert.Equal(1, recovered);
        Assert.Equal(1, deleted);
        Assert.False(File.Exists(path));
        Assert.Empty(await DbContextForAssert.AttachmentUploadTickets.ToListAsync());
        Assert.Empty(await DbContextForAssert.CardAttachments.ToListAsync());
        Assert.Contains(await DbContextForAssert.AttachmentTransferAudits.ToListAsync(),
            x => x.Outcome == AttachmentTransferAuditOutcome.UploadRecoveredAtStartup);
    }

    private async Task<(int BoardId, int CardId, int AttachmentId)> ArrangeAttachment()
    {
        var board = CreateBoard().AddColumn("Todo").AddCard("Card").Build();
        var card = board.GetCard("Card");
        var attachment = await ResolveService<ICardAttachmentService>().UploadAsync(board.BoardId, card.BoardCardId, ActorUserId,
            "original.bin", null, new MemoryStream([0, 255, 13, 10, 42]));
        Assert.True(attachment.Success, attachment.Message);
        return (board.BoardId, card.BoardCardId, attachment.Data!.Id);
    }

    private async Task<AttachmentDownloadTicket> Issue(int boardId, int attachmentId)
    {
        var result = await ResolveService<IAttachmentTransferService>().IssueDownloadAsync(boardId, attachmentId, ActorUserId, Credential);
        Assert.True(result.Success, result.Message);
        return result.Data!;
    }

    private async Task<AttachmentUploadTicket> IssueUpload(int boardId, int cardId, string fileName, long byteLength)
    {
        var result = await ResolveService<IAttachmentTransferService>().IssueUploadAsync(
            boardId, cardId, ActorUserId, fileName, "application/octet-stream", byteLength, Credential);
        Assert.True(result.Success, result.Message);
        return result.Data!;
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class TestCredentials : IAttachmentTransferCredentialValidator
    {
        public bool Valid { get; set; } = true;
        public DateTime? Expires { get; set; }
        public List<string> RequiredScopes { get; } = [];
        public Task<ApiResult<DateTime?>> ValidateAsync(int actorUserId, AttachmentTransferCredential credential,
            string requiredScope, CancellationToken cancellationToken = default)
        {
            RequiredScopes.Add(requiredScope);
            return Task.FromResult(Valid ? ApiResults.Ok(Expires) : ApiResults.Unauthorized<DateTime?>("Revoked"));
        }
    }

    private sealed class TestAuditRepositoryState
    {
        public AttachmentTransferAuditOutcome? OutcomeToThrow { get; set; }
    }

    private sealed class TestAuditRepository(
        IAmbientDbContextLocator locator, TestAuditRepositoryState state) : IAttachmentTransferAuditRepository
    {
        private BoardOilDbContext DbContext => locator.Get<BoardOilDbContext>() ??
            throw new InvalidOperationException("No ambient database context.");

        public IQueryable<EntityAttachmentTransferAudit> Query() => DbContext.AttachmentTransferAudits;
        public EntityAttachmentTransferAudit? Get(int id) => DbContext.AttachmentTransferAudits.Find(id);
        public void Add(EntityAttachmentTransferAudit entity)
        {
            if (state.OutcomeToThrow == entity.Outcome) { throw new InvalidOperationException("Audit write failed."); }
            DbContext.AttachmentTransferAudits.Add(entity);
        }
        public void AddRange(IEnumerable<EntityAttachmentTransferAudit> entities) =>
            DbContext.AttachmentTransferAudits.AddRange(entities);
        public void Remove(EntityAttachmentTransferAudit entity) => DbContext.AttachmentTransferAudits.Remove(entity);
        public void RemoveRange(IEnumerable<EntityAttachmentTransferAudit> entities) =>
            DbContext.AttachmentTransferAudits.RemoveRange(entities);
    }

    private sealed class ThrowOnReadStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Stream was read.");
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Stream was read.");
    }

    private sealed class BlockingReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        private readonly TaskCompletionSource _readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task ReadStarted => _readStarted.Task;
        public void Release() => _release.TrySetResult();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _readStarted.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }

    private sealed class ActionAfterFirstReadStream(byte[] bytes, Action action) : MemoryStream(bytes)
    {
        private bool _acted;

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var count = await base.ReadAsync(buffer, cancellationToken);
            if (!_acted && count > 0)
            {
                _acted = true;
                action();
            }
            return count;
        }
    }
}
