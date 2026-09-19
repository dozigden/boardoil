using System.Security.Cryptography;
using System.Text;
using BoardOil.Abstractions.Attachment;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Attachment;
using BoardOil.Data.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoardOil.Services.Attachment;

public sealed class AttachmentTransferService(
    IAttachmentDownloadTicketRepository downloadTickets, IAttachmentUploadTicketRepository uploadTickets,
    IAttachmentTransferAuditRepository audits, IAttachmentRepository attachments,
    IAttachmentTransferCredentialValidator credentials, CardAttachmentService attachmentService,
    IBoardAuthorisationService authorisation, IDbContextScopeFactory scopes, AttachmentStorageOptions options,
    TimeProvider clock, ILogger<AttachmentTransferService> logger) : IAttachmentTransferService
{
    private static readonly SemaphoreSlim DownloadPersistenceLock = new(1, 1);

    public async Task<ApiResult<AttachmentDownloadTicket>> IssueDownloadAsync(int boardId, int attachmentId, int actorUserId,
        AttachmentTransferCredential credential, CancellationToken cancellationToken = default)
    {
        using var scope = scopes.CreateWithTransaction(System.Data.IsolationLevel.Serializable);
        var validity = await credentials.ValidateAsync(actorUserId, credential, MachinePatScopes.McpRead, cancellationToken);
        if (!validity.Success) { return ApiErrors.Unauthorized(validity.Message!); }
        if (!await authorisation.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardAccess))
        {
            return ApiErrors.Forbidden("You do not have access to this board.");
        }
        var attachment = await attachments.Query().Include(x => x.Card).Include(x => x.ArchivedCard)
            .SingleOrDefaultAsync(x => x.Id == attachmentId && x.State == AttachmentState.Ready &&
                ((x.Card != null && x.Card.BoardId == boardId) || (x.ArchivedCard != null && x.ArchivedCard.BoardId == boardId)), cancellationToken);
        if (attachment is null) { return ApiErrors.NotFound("Attachment not found."); }

        var now = clock.GetUtcNow().UtcDateTime;
        var expires = ResolveExpiry(now, validity.Data);
        if (expires <= now) { return InvalidTicket<AttachmentDownloadTicket>(); }
        var secret = CreateSecret();
        var ticket = new EntityAttachmentDownloadTicket
        {
            SecretHash = HashHex(secret), ActorUserId = actorUserId,
            BoardId = boardId, AttachmentId = attachmentId, CardId = attachment.CardId,
            ArchivedCardId = attachment.ArchivedCardId,
            CardNumber = attachment.Card?.BoardCardId ?? attachment.ArchivedCard!.OriginalCardId,
            PersonalAccessTokenId = credential.PersonalAccessTokenId, OAuthTokenId = credential.OAuthTokenId,
            OAuthAuthorizationId = credential.OAuthAuthorizationId, CreatedAtUtc = now, ExpiresAtUtc = expires
        };
        downloadTickets.Add(ticket);
        await scope.SaveChangesAsync(cancellationToken);
        await RecordDownloadAuditAsync(ticket, AttachmentTransferAuditOutcome.Issued, now, cancellationToken);
        logger.LogInformation("Issued attachment download ticket {TicketId} for user {UserId}, board {BoardId}, attachment {AttachmentId}.",
            ticket.Id, actorUserId, boardId, attachmentId);
        return new AttachmentDownloadTicket(ticket.Id, secret, expires);
    }

    public async Task<ApiResult<AttachmentUploadTicket>> IssueUploadAsync(int boardId, int cardId, int actorUserId,
        string fileName, string? contentType, long byteLength, AttachmentTransferCredential credential,
        CancellationToken cancellationToken = default)
    {
        if (byteLength < 0)
        {
            return ApiErrors.ValidationFailed([new("byteLength", "'byteLength' must be zero or greater.")]);
        }
        if (byteLength > options.MaxUploadByteLength)
        {
            return new ApiError(413, $"Attachment must be {options.MaxUploadByteLength} bytes or smaller.");
        }

        try
        {
            using var scope = scopes.Create();
            ApiResult<AttachmentUploadTicket>? result = null;
            EntityAttachmentUploadTicket? issuedTicket = null;
            await scope.Transaction(async (transactionScope, transaction) =>
            {
                var validity = await credentials.ValidateAsync(
                    actorUserId, credential, MachinePatScopes.McpWrite, cancellationToken);
                if (!validity.Success)
                {
                    result = ApiResults.Unauthorized<AttachmentUploadTicket>(validity.Message!);
                    return;
                }
                var access = await attachmentService.CheckUploadAccessAsync(boardId, cardId, actorUserId);
                if (!access.Success)
                {
                    result = new ApiError(access.StatusCode, access.Message!);
                    return;
                }

                var now = clock.GetUtcNow().UtcDateTime;
                var expires = ResolveExpiry(now, validity.Data);
                if (expires <= now)
                {
                    result = InvalidTicket<AttachmentUploadTicket>();
                    return;
                }
                var attachment = await attachmentService.ReserveUploadAsync(
                    fileName, contentType, actorUserId, access.Data, now, cancellationToken);
                var secret = CreateSecret();
                issuedTicket = new EntityAttachmentUploadTicket
                {
                    SecretHash = HashHex(secret), ActorUserId = actorUserId, BoardId = boardId,
                    CardId = access.Data, CardNumber = cardId, Attachment = attachment,
                    OriginalFileName = attachment.OriginalFileName, ContentType = attachment.ContentType,
                    DeclaredByteLength = byteLength, PersonalAccessTokenId = credential.PersonalAccessTokenId,
                    OAuthTokenId = credential.OAuthTokenId, OAuthAuthorizationId = credential.OAuthAuthorizationId,
                    State = AttachmentUploadTicketState.Issued, CreatedAtUtc = now, ExpiresAtUtc = expires
                };
                uploadTickets.Add(issuedTicket);
                await transactionScope.SaveChangesAsync(cancellationToken);
                audits.Add(CreateAudit(issuedTicket, AttachmentTransferAuditOutcome.Issued, now));
                await transactionScope.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync();
                result = new AttachmentUploadTicket(
                    issuedTicket.Id, secret, issuedTicket.OriginalFileName, issuedTicket.ContentType, byteLength, expires);
            });
            if (!result!.Success) { return result; }
            logger.LogInformation("Issued attachment upload ticket {TicketId} for user {UserId}, board {BoardId}, attachment {AttachmentId}.",
                issuedTicket!.Id, actorUserId, boardId, issuedTicket.AttachmentId);
            return result;
        }
        catch (AttachmentNameConflictException exception) { return new ApiError(409, exception.Message); }
        catch (ArgumentException exception) { return ApiErrors.ValidationFailed([new("fileName", exception.Message)]); }
    }

    public async Task<ApiResult<AttachmentDownload>> DownloadAsync(int ticketId, string secret, CancellationToken cancellationToken = default)
    {
        var startedAt = clock.GetUtcNow().UtcDateTime;
        if (!HasValidTicketShape(ticketId, secret)) { return InvalidTicket<AttachmentDownload>(); }

        DownloadAdmission admission;
        await DownloadPersistenceLock.WaitAsync(cancellationToken);
        try
        {
            admission = await AdmitDownloadAsync(ticketId, secret, startedAt, cancellationToken);
            if (admission.Ticket is null || admission.Outcome is null) { return admission.Result; }
            try
            {
                await PersistDownloadAuditAsync(
                    admission.Ticket, admission.Outcome.Value, startedAt, cancellationToken);
            }
            catch
            {
                if (admission.Result.Success) { await admission.Result.Data!.Content.DisposeAsync(); }
                throw;
            }
        }
        finally
        {
            DownloadPersistenceLock.Release();
        }
        if (admission.Result.Success)
        {
            logger.LogInformation("Redeemed attachment download ticket {TicketId} for user {UserId}, board {BoardId}, attachment {AttachmentId}.",
                admission.Ticket.Id, admission.Ticket.ActorUserId, admission.Ticket.BoardId, admission.Ticket.AttachmentId);
        }
        return admission.Result;
    }

    public async Task<ApiResult<CardAttachmentDto>> UploadAsync(int ticketId, string secret, string? contentType,
        long? contentLength, Stream content, CancellationToken cancellationToken = default)
    {
        var startedAt = clock.GetUtcNow().UtcDateTime;
        if (!HasValidTicketShape(ticketId, secret)) { return InvalidTicket<CardAttachmentDto>(); }
        var inspection = await InspectUploadAsync(ticketId, secret, contentType, contentLength, startedAt, cancellationToken);
        if (inspection.Ticket is null) { return inspection.Result!; }
        if (inspection.Result is not null)
        {
            await TryRecordUploadAuditAsync(
                inspection.Ticket, inspection.Outcome!.Value, startedAt, CancellationToken.None);
            return inspection.Result;
        }

        var ticket = inspection.Ticket;
        if (!await ClaimUploadAsync(ticket, startedAt, cancellationToken))
        {
            await TryRecordUploadAuditAsync(
                ticket, AttachmentTransferAuditOutcome.UploadAlreadyClaimed, startedAt, CancellationToken.None);
            return UploadAlreadyClaimed();
        }

        PreparedAttachmentFile prepared;
        try
        {
            prepared = await attachmentService.WritePreparedAsync(
                new PreparedAttachmentFile(ticket.AttachmentId, ticket.Attachment.StorageKey), content,
                options.MaxUploadByteLength, ticket.DeclaredByteLength, cancellationToken);
        }
        catch (AttachmentLengthException exception)
        {
            await FailUploadAsync(ticket.Id, clock.GetUtcNow().UtcDateTime, CancellationToken.None);
            return ApiErrors.ValidationFailed([new("content", exception.Message)]);
        }
        catch (AttachmentSizeException exception)
        {
            await FailUploadAsync(ticket.Id, clock.GetUtcNow().UtcDateTime, CancellationToken.None);
            return new ApiError(413, exception.Message);
        }
        catch
        {
            await FailUploadAsync(ticket.Id, clock.GetUtcNow().UtcDateTime, CancellationToken.None);
            throw;
        }

        var completion = await CompleteUploadAsync(ticket, prepared, cancellationToken);
        await TryRecordUploadAuditAsync(
            ticket, completion.Outcome, clock.GetUtcNow().UtcDateTime, CancellationToken.None);
        if (!completion.Result.Success) { return completion.Result; }
        await attachmentService.PublishUploadEventsAsync(ticket.BoardId, ticket.CardNumber, ticket.AttachmentId);
        logger.LogInformation("Completed attachment upload ticket {TicketId} for user {UserId}, board {BoardId}, attachment {AttachmentId}.",
            ticket.Id, ticket.ActorUserId, ticket.BoardId, ticket.AttachmentId);
        return completion.Result;
    }

    private async Task<DownloadAdmission> AdmitDownloadAsync(int ticketId, string secret, DateTime startedAt,
        CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateWithTransaction(System.Data.IsolationLevel.Serializable);
        var ticket = await downloadTickets.Query().SingleOrDefaultAsync(x => x.Id == ticketId, cancellationToken);
        if (ticket is null || !SecretMatches(secret, ticket.SecretHash))
        {
            return new(InvalidTicket<AttachmentDownload>(), null, null);
        }
        if (ticket.ExpiresAtUtc <= startedAt)
        {
            return new(InvalidTicket<AttachmentDownload>(), ticket, AttachmentTransferAuditOutcome.Expired);
        }
        var validity = await credentials.ValidateAsync(ticket.ActorUserId,
            Credential(ticket.PersonalAccessTokenId, ticket.OAuthTokenId, ticket.OAuthAuthorizationId),
            MachinePatScopes.McpRead, cancellationToken);
        if (!validity.Success)
        {
            return new(InvalidTicket<AttachmentDownload>(), ticket, AttachmentTransferAuditOutcome.CredentialInvalid);
        }
        var ownerMatches = await attachments.Query().AnyAsync(x => x.Id == ticket.AttachmentId && x.State == AttachmentState.Ready &&
            x.CardId == ticket.CardId && x.ArchivedCardId == ticket.ArchivedCardId &&
            ((x.Card != null && x.Card.BoardId == ticket.BoardId && x.Card.BoardCardId == ticket.CardNumber) ||
             (x.ArchivedCard != null && x.ArchivedCard.BoardId == ticket.BoardId && x.ArchivedCard.OriginalCardId == ticket.CardNumber)), cancellationToken);
        if (!ownerMatches)
        {
            return new(InvalidTicket<AttachmentDownload>(), ticket, AttachmentTransferAuditOutcome.OwnerChanged);
        }
        var download = await attachmentService.DownloadAsync(ticket.BoardId, ticket.AttachmentId, ticket.ActorUserId);
        if (!download.Success)
        {
            var outcome = download.StatusCode == 403
                ? AttachmentTransferAuditOutcome.AccessDenied
                : AttachmentTransferAuditOutcome.AttachmentUnavailable;
            return new(new ApiError(download.StatusCode, download.Message!), ticket, outcome);
        }
        return new(download, ticket, AttachmentTransferAuditOutcome.DownloadAdmitted);
    }

    private async Task<UploadInspection> InspectUploadAsync(int ticketId, string secret, string? contentType,
        long? contentLength, DateTime startedAt, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateWithTransaction(System.Data.IsolationLevel.Serializable);
        var ticket = await uploadTickets.Query().Include(x => x.Attachment)
            .SingleOrDefaultAsync(x => x.Id == ticketId, cancellationToken);
        if (ticket is null || !SecretMatches(secret, ticket.SecretHash))
        {
            return new(InvalidTicket<CardAttachmentDto>(), null, null);
        }
        if (ticket.ExpiresAtUtc <= startedAt)
        {
            await FailIssuedUploadAsync(ticket, scope, cancellationToken);
            return new(InvalidTicket<CardAttachmentDto>(), ticket, AttachmentTransferAuditOutcome.Expired);
        }
        var validity = await credentials.ValidateAsync(ticket.ActorUserId,
            Credential(ticket.PersonalAccessTokenId, ticket.OAuthTokenId, ticket.OAuthAuthorizationId),
            MachinePatScopes.McpWrite, cancellationToken);
        if (!validity.Success)
        {
            await FailIssuedUploadAsync(ticket, scope, cancellationToken);
            return new(InvalidTicket<CardAttachmentDto>(), ticket, AttachmentTransferAuditOutcome.CredentialInvalid);
        }
        var access = await attachmentService.CheckUploadAccessAsync(ticket.BoardId, ticket.CardNumber, ticket.ActorUserId);
        if (!access.Success || access.Data != ticket.CardId)
        {
            await FailIssuedUploadAsync(ticket, scope, cancellationToken);
            return new(InvalidTicket<CardAttachmentDto>(), ticket, AttachmentTransferAuditOutcome.AccessDenied);
        }
        if (ticket.State is AttachmentUploadTicketState.Uploading or AttachmentUploadTicketState.Failed)
        {
            return new(UploadAlreadyClaimed(), ticket, AttachmentTransferAuditOutcome.UploadAlreadyClaimed);
        }
        if (ticket.State == AttachmentUploadTicketState.Completed)
        {
            if (!UploadOwnerMatches(ticket, AttachmentState.Ready))
            {
                return new(InvalidTicket<CardAttachmentDto>(), ticket, AttachmentTransferAuditOutcome.OwnerChanged);
            }
            return new(ApiResults.Ok(CardAttachmentService.ToDto(ticket.Attachment)), ticket,
                AttachmentTransferAuditOutcome.UploadCompletedReturned);
        }
        if (!UploadOwnerMatches(ticket, AttachmentState.Pending))
        {
            await FailIssuedUploadAsync(ticket, scope, cancellationToken);
            return new(InvalidTicket<CardAttachmentDto>(), ticket, AttachmentTransferAuditOutcome.OwnerChanged);
        }
        if (contentLength != ticket.DeclaredByteLength || AttachmentFileMetadata.ContentType(contentType) != ticket.ContentType)
        {
            return new((ApiResult<CardAttachmentDto>)ApiErrors.ValidationFailed([
                new("content", "Content-Length and Content-Type must match the upload ticket.")]),
                ticket, AttachmentTransferAuditOutcome.UploadRequestInvalid);
        }
        return new(null, ticket, null);
    }

    private async Task FailIssuedUploadAsync(EntityAttachmentUploadTicket ticket, IDbContextScope scope,
        CancellationToken cancellationToken)
    {
        if (ticket.State != AttachmentUploadTicketState.Issued) { return; }
        MarkUploadFailed(ticket);
        await scope.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> ClaimUploadAsync(EntityAttachmentUploadTicket ticket, DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        using var suppressed = scopes.SuppressAmbientContext();
        using var scope = scopes.CreateWithTransaction(System.Data.IsolationLevel.Serializable);
        var affected = await uploadTickets.Query()
            .Where(x => x.Id == ticket.Id && x.State == AttachmentUploadTicketState.Issued)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.State, AttachmentUploadTicketState.Uploading), cancellationToken);
        if (affected == 1)
        {
            audits.Add(CreateAudit(ticket, AttachmentTransferAuditOutcome.UploadClaimed, occurredAtUtc));
        }
        await scope.SaveChangesAsync(cancellationToken);
        return affected == 1;
    }

    private async Task<UploadCompletion> CompleteUploadAsync(EntityAttachmentUploadTicket inspectedTicket,
        PreparedAttachmentFile prepared, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateWithTransaction(System.Data.IsolationLevel.Serializable);
        var ticket = await uploadTickets.Query().Include(x => x.Attachment)
            .SingleOrDefaultAsync(x => x.Id == inspectedTicket.Id, cancellationToken);
        if (ticket is null || ticket.State != AttachmentUploadTicketState.Uploading)
        {
            return new(UploadAlreadyClaimed(), AttachmentTransferAuditOutcome.UploadAlreadyClaimed);
        }
        var validity = await credentials.ValidateAsync(ticket.ActorUserId,
            Credential(ticket.PersonalAccessTokenId, ticket.OAuthTokenId, ticket.OAuthAuthorizationId),
            MachinePatScopes.McpWrite, cancellationToken);
        if (!validity.Success)
        {
            MarkUploadFailed(ticket);
            await scope.SaveChangesAsync(cancellationToken);
            return new(InvalidTicket<CardAttachmentDto>(), AttachmentTransferAuditOutcome.CredentialInvalid);
        }
        if (!UploadOwnerMatches(ticket, AttachmentState.Pending) ||
            !await attachmentService.PrepareUploadPublicationAsync(
                ticket.BoardId, ticket.CardNumber, ticket.CardId, ticket.ActorUserId))
        {
            MarkUploadFailed(ticket);
            await scope.SaveChangesAsync(cancellationToken);
            return new(new ApiError(409, "The card or its permissions changed. Request a new upload ticket."),
                AttachmentTransferAuditOutcome.OwnerChanged);
        }
        var attachment = attachmentService.Publish(prepared);
        ticket.State = AttachmentUploadTicketState.Completed;
        ticket.CompletedAtUtc = clock.GetUtcNow().UtcDateTime;
        await scope.SaveChangesAsync(cancellationToken);
        return new(ApiResults.Created(CardAttachmentService.ToDto(attachment)), AttachmentTransferAuditOutcome.UploadCompleted);
    }

    private async Task FailUploadAsync(int ticketId, DateTime occurredAtUtc, CancellationToken cancellationToken)
    {
        EntityAttachmentUploadTicket? ticket;
        using (scopes.SuppressAmbientContext())
        using (var scope = scopes.CreateWithTransaction(System.Data.IsolationLevel.Serializable))
        {
            ticket = await uploadTickets.Query().Include(x => x.Attachment)
                .SingleOrDefaultAsync(x => x.Id == ticketId, cancellationToken);
            if (ticket is null || ticket.State == AttachmentUploadTicketState.Completed) { return; }
            MarkUploadFailed(ticket);
            await scope.SaveChangesAsync(cancellationToken);
        }
        await TryRecordUploadAuditAsync(
            ticket, AttachmentTransferAuditOutcome.UploadFailed, occurredAtUtc, CancellationToken.None);
    }

    private void MarkUploadFailed(EntityAttachmentUploadTicket ticket)
    {
        ticket.State = AttachmentUploadTicketState.Failed;
        ticket.FailedAtUtc = clock.GetUtcNow().UtcDateTime;
        if (ticket.Attachment.State != AttachmentState.Ready)
        {
            CardAttachmentService.MarkForDeletion(ticket.Attachment);
        }
    }

    public async Task<int> CleanupAtStartupAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopes.Create();
        var now = clock.GetUtcNow().UtcDateTime;
        var expiredDownloads = await downloadTickets.Query().Where(x => x.ExpiresAtUtc <= now).ToListAsync(cancellationToken);
        var incompleteUploads = await uploadTickets.Query().Include(x => x.Attachment)
            .Where(x => x.State != AttachmentUploadTicketState.Completed).ToListAsync(cancellationToken);
        var expiredCompletedUploads = await uploadTickets.Query()
            .Where(x => x.State == AttachmentUploadTicketState.Completed && x.ExpiresAtUtc <= now).ToListAsync(cancellationToken);
        var auditCutoff = now.AddDays(-AttachmentTransferAuditRetention.Days);
        var oldAudits = await audits.Query().Where(x => x.OccurredAtUtc < auditCutoff).ToListAsync(cancellationToken);
        foreach (var ticket in expiredDownloads) { downloadTickets.Remove(ticket); }
        foreach (var ticket in incompleteUploads)
        {
            if (ticket.State is AttachmentUploadTicketState.Issued or AttachmentUploadTicketState.Uploading)
            {
                audits.Add(CreateAudit(ticket, AttachmentTransferAuditOutcome.UploadRecoveredAtStartup, now));
            }
            MarkUploadFailed(ticket);
        }
        foreach (var ticket in expiredCompletedUploads) { uploadTickets.Remove(ticket); }
        foreach (var audit in oldAudits) { audits.Remove(audit); }
        await scope.SaveChangesAsync(cancellationToken);
        return expiredDownloads.Count + incompleteUploads.Count + expiredCompletedUploads.Count;
    }

    private async Task RecordDownloadAuditAsync(EntityAttachmentDownloadTicket ticket,
        AttachmentTransferAuditOutcome outcome, DateTime occurredAtUtc, CancellationToken cancellationToken)
    {
        await DownloadPersistenceLock.WaitAsync(cancellationToken);
        try
        {
            await PersistDownloadAuditAsync(ticket, outcome, occurredAtUtc, cancellationToken);
        }
        finally
        {
            DownloadPersistenceLock.Release();
        }
    }

    private async Task PersistDownloadAuditAsync(EntityAttachmentDownloadTicket ticket,
        AttachmentTransferAuditOutcome outcome, DateTime occurredAtUtc, CancellationToken cancellationToken)
    {
        using var suppressed = scopes.SuppressAmbientContext();
        using var scope = scopes.Create();
        audits.Add(CreateAudit(ticket, outcome, occurredAtUtc));
        await scope.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordAuditAsync(EntityAttachmentUploadTicket ticket,
        AttachmentTransferAuditOutcome outcome, DateTime occurredAtUtc, CancellationToken cancellationToken)
    {
        using var suppressed = scopes.SuppressAmbientContext();
        using var scope = scopes.Create();
        audits.Add(CreateAudit(ticket, outcome, occurredAtUtc));
        await scope.SaveChangesAsync(cancellationToken);
    }

    private async Task TryRecordUploadAuditAsync(EntityAttachmentUploadTicket ticket,
        AttachmentTransferAuditOutcome outcome, DateTime occurredAtUtc, CancellationToken cancellationToken)
    {
        try
        {
            await RecordAuditAsync(ticket, outcome, occurredAtUtc, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Failed to record attachment upload audit {Outcome} for ticket {TicketId}.", outcome, ticket.Id);
        }
    }

    private static EntityAttachmentTransferAudit CreateAudit(EntityAttachmentDownloadTicket ticket,
        AttachmentTransferAuditOutcome outcome, DateTime occurredAtUtc) =>
        CreateAudit(ticket.Id, ticket.ActorUserId, ticket.BoardId, ticket.AttachmentId,
            ticket.PersonalAccessTokenId, AttachmentTransferOperation.Download, outcome, occurredAtUtc);

    private static EntityAttachmentTransferAudit CreateAudit(EntityAttachmentUploadTicket ticket,
        AttachmentTransferAuditOutcome outcome, DateTime occurredAtUtc) =>
        CreateAudit(ticket.Id, ticket.ActorUserId, ticket.BoardId, ticket.AttachmentId,
            ticket.PersonalAccessTokenId, AttachmentTransferOperation.Upload, outcome, occurredAtUtc);

    private static EntityAttachmentTransferAudit CreateAudit(int ticketId, int actorUserId, int boardId,
        int attachmentId, int? personalAccessTokenId, AttachmentTransferOperation operation,
        AttachmentTransferAuditOutcome outcome, DateTime occurredAtUtc) =>
        new()
        {
            TicketId = ticketId,
            ActorUserId = actorUserId,
            BoardId = boardId,
            AttachmentId = attachmentId,
            Operation = operation,
            CredentialType = personalAccessTokenId.HasValue
                ? AttachmentTransferCredentialType.PersonalAccessToken
                : AttachmentTransferCredentialType.OAuth,
            Outcome = outcome,
            OccurredAtUtc = occurredAtUtc
        };

    private static bool UploadOwnerMatches(EntityAttachmentUploadTicket ticket, AttachmentState state) =>
        ticket.Attachment.Id == ticket.AttachmentId && ticket.Attachment.State == state &&
        ticket.Attachment.CardId == ticket.CardId && ticket.Attachment.ArchivedCardId is null &&
        ticket.Attachment.OriginalFileName == ticket.OriginalFileName && ticket.Attachment.ContentType == ticket.ContentType;

    private static DateTime ResolveExpiry(DateTime now, DateTime? credentialExpiry)
    {
        var expires = now.AddMinutes(2);
        if (credentialExpiry is { } value && value < expires) { expires = value; }
        return expires;
    }

    private static AttachmentTransferCredential Credential(int? personalAccessTokenId, string? oauthTokenId,
        string? oauthAuthorizationId) => new(personalAccessTokenId, oauthTokenId, oauthAuthorizationId);
    private static string CreateSecret() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    private static bool HasValidTicketShape(int ticketId, string secret) => ticketId > 0 && secret.Length == 64;
    private static bool SecretMatches(string secret, string expectedHash) =>
        CryptographicOperations.FixedTimeEquals(Hash(secret), Convert.FromHexString(expectedHash));
    private static string HashHex(string secret) => Convert.ToHexString(Hash(secret));
    private static byte[] Hash(string secret) => SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    private static ApiResult<T> InvalidTicket<T>() =>
        ApiResults.Unauthorized<T>("The attachment transfer ticket is invalid or expired.");
    private static ApiError UploadAlreadyClaimed() =>
        new(409, "This upload ticket has already been claimed. Request a new ticket if it did not complete.");

    private sealed record DownloadAdmission(ApiResult<AttachmentDownload> Result,
        EntityAttachmentDownloadTicket? Ticket, AttachmentTransferAuditOutcome? Outcome);
    private sealed record UploadInspection(ApiResult<CardAttachmentDto>? Result,
        EntityAttachmentUploadTicket? Ticket, AttachmentTransferAuditOutcome? Outcome);
    private sealed record UploadCompletion(ApiResult<CardAttachmentDto> Result, AttachmentTransferAuditOutcome Outcome);
}
