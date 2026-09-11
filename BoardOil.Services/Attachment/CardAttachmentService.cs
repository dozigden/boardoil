using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using BoardOil.Abstractions;
using BoardOil.Abstractions.Attachment;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Card;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Attachment;
using BoardOil.Data.Abstractions.Card;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Image;
using BoardOil.Services.Card;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Services.Attachment;

public sealed class CardAttachmentService(
    IAttachmentRepository attachments, ICardRepository cards, IArchivedCardRepository archives,
    IBoardAuthorisationService authorisation, IAttachmentStorageService storage, AttachmentStorageOptions options,
    IDbContextScopeFactory scopes, IBoardEvents events, IImageRepository images, ILogger<CardAttachmentService>? logger = null) : ICardAttachmentService
{
    public async Task<ApiResult<CardAttachmentListDto>> ListAsync(int boardId, int cardId, bool archived, int actorUserId)
    {
        using var scope = scopes.CreateReadOnly();
        if (!await authorisation.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardAccess))
        {
            return ApiErrors.Forbidden("You do not have access to this board.");
        }
        IQueryable<EntityCardAttachment> query;
        if (archived)
        {
            var card = await archives.GetByBoardCardIdAsync(boardId, cardId);
            if (card is null) { return ApiErrors.NotFound("Archived card not found."); }
            query = attachments.Query().Where(x => x.ArchivedCardId == card.Id);
        }
        else
        {
            var card = await cards.GetWithTagsAndBoardAsync(boardId, cardId);
            if (card is null) { return ApiErrors.NotFound("Card not found."); }
            query = attachments.Query().Where(x => x.CardId == card.Id);
        }
        var items = await query.Where(x => x.State == AttachmentState.Ready).OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).ToListAsync();
        return new CardAttachmentListDto(items.Select(ToDto).ToList(), options.MaxUploadByteLength);
    }

    public async Task<ApiResult<int>> CheckUploadAccessAsync(int boardId, int cardId, int actorUserId)
    {
        using var scope = scopes.CreateReadOnly();
        if (!await authorisation.HasPermissionAsync(boardId, actorUserId, BoardPermission.CardUpdate))
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }
        var card = await cards.GetWithTagsAndBoardAsync(boardId, cardId);
        if (card is null) { return ApiErrors.NotFound("Card not found."); }
        return ApiResults.Ok(card.Id);
    }

    public async Task<ApiResult<CardAttachmentDto>> UploadAsync(int boardId, int cardId, int actorUserId,
        string fileName, string? contentType, Stream content, CancellationToken cancellationToken = default)
    {
        var check = await CheckUploadAccessAsync(boardId, cardId, actorUserId);
        if (!check.Success) { return new ApiError(check.StatusCode, check.Message!); }
        PreparedAttachmentFile? prepared = null;
        var published = false;
        try
        {
            var name = AttachmentFileMetadata.FileName(fileName);
            prepared = await PrepareAsync(content, name, contentType, actorUserId, check.Data, cancellationToken: cancellationToken);
            using var scope = scopes.Create();
            EntityCardAttachment? attachment = null;
            CardDto? updatedCard = null;
            await scope.Transaction(async (transactionScope, transaction) =>
            {
                if (!await authorisation.HasPermissionAsync(boardId, actorUserId, BoardPermission.CardUpdate))
                {
                    throw new AttachmentOwnerChangedException();
                }
                var card = await cards.GetWithTagsAndBoardAsync(boardId, cardId);
                if (card is null || card.Id != check.Data) { throw new AttachmentOwnerChangedException(); }
                attachment = Publish(prepared);
                attachment.Card = card;
                card.CardUpdatedUtc = DateTime.UtcNow;
                await transactionScope.SaveChangesAsync(cancellationToken);
                updatedCard = await CardDtoEnrichment.EnrichAssignedUserImageAsync(card.ToCardDto(), images);
                await transaction.CommitAsync();
            });
            published = true;
            var dto = ToDto(attachment!);
            await events.CardUpdatedAsync(boardId, updatedCard!);
            await events.AttachmentAddedAsync(boardId, cardId, dto);
            return ApiResults.Created(dto);
        }
        catch (AttachmentNameConflictException exception) { return new ApiError(409, exception.Message); }
        catch (ArgumentException exception) { return ApiErrors.ValidationFailed([new("file", exception.Message)]); }
        catch (AttachmentSizeException exception) { return new ApiError(413, exception.Message); }
        catch (AttachmentOwnerChangedException) { return new ApiError(409, "The card or its permissions changed. Reload and retry."); }
        finally
        {
            if (prepared is not null && !published) { await AbandonAsync(prepared.Id); }
        }
    }

    public async Task<ApiResult<AttachmentDownload>> DownloadAsync(int boardId, int attachmentId, int actorUserId)
    {
        using var scope = scopes.CreateReadOnly();
        if (!await authorisation.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardAccess))
        {
            return ApiErrors.Forbidden("You do not have access to this board.");
        }
        var attachment = await attachments.Query().Where(x => x.Id == attachmentId && x.State == AttachmentState.Ready &&
            ((x.Card != null && x.Card.BoardId == boardId) || (x.ArchivedCard != null && x.ArchivedCard.BoardId == boardId)))
            .SingleOrDefaultAsync();
        if (attachment is null) { return ApiErrors.NotFound("Attachment not found."); }
        try { return new AttachmentDownload(attachment.OriginalFileName, storage.OpenRead(attachment.StorageKey)); }
        catch (FileNotFoundException) { return ApiErrors.NotFound("The attachment file is unavailable."); }
        catch (DirectoryNotFoundException) { return ApiErrors.NotFound("The attachment file is unavailable."); }
    }

    public async Task<ApiResult> DeleteAsync(int boardId, int cardId, int attachmentId, int actorUserId)
    {
        using var scope = scopes.CreateWithTransaction(System.Data.IsolationLevel.Serializable);
        if (!await authorisation.HasPermissionAsync(boardId, actorUserId, BoardPermission.CardUpdate))
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }
        var card = await cards.GetWithTagsAndBoardAsync(boardId, cardId);
        if (card is null) { return ApiErrors.NotFound("Card not found."); }
        var attachment = await attachments.Query().SingleOrDefaultAsync(x => x.Id == attachmentId && x.CardId == card.Id && x.State == AttachmentState.Ready);
        if (attachment is null) { return ApiErrors.NotFound("Attachment not found."); }
        MarkForDeletion(attachment);
        card.CardUpdatedUtc = DateTime.UtcNow;
        await scope.SaveChangesAsync();
        await DeleteFilesAsync([attachment.StorageKey]);
        await events.CardUpdatedAsync(boardId, await CardDtoEnrichment.EnrichAssignedUserImageAsync(card.ToCardDto(), images));
        await events.AttachmentDeletedAsync(boardId, cardId, attachmentId);
        return ApiResults.Ok();
    }

    // These ownership/deletion methods participate in the caller's transaction; they do not save.
    public async Task ArchiveAsync(EntityBoardCard card, EntityArchivedCard archive)
    {
        foreach (var attachment in await attachments.Query().Where(x => x.CardId == card.Id).ToListAsync())
        {
            attachment.Card = null;
            attachment.CardId = null;
            attachment.ArchivedCard = archive;
        }
    }

    public async Task RestoreAsync(EntityArchivedCard archive, EntityBoardCard card)
    {
        foreach (var attachment in await attachments.Query().Where(x => x.ArchivedCardId == archive.Id).ToListAsync())
        {
            attachment.ArchivedCard = null;
            attachment.ArchivedCardId = null;
            attachment.Card = card;
        }
    }

    public Task<IReadOnlyList<string>> DeleteForCardsAsync(IReadOnlyList<int> databaseCardIds) =>
        MarkForDeletionAsync(attachments.Query().Where(x => x.CardId.HasValue && databaseCardIds.Contains(x.CardId.Value)));

    public Task<IReadOnlyList<string>> DeleteForColumnAsync(int columnId) =>
        MarkForDeletionAsync(attachments.Query().Where(x => x.Card != null && x.Card.BoardColumnId == columnId));

    public Task<IReadOnlyList<string>> DeleteForBoardAsync(int boardId) =>
        MarkForDeletionAsync(attachments.Query().Where(x => (x.Card != null && x.Card.BoardId == boardId)
            || (x.ArchivedCard != null && x.ArchivedCard.BoardId == boardId)));

    private async Task<IReadOnlyList<string>> MarkForDeletionAsync(IQueryable<EntityCardAttachment> query)
    {
        var items = await query.ToListAsync();
        // Capture Ready paths before changing state. Pending writers are reclaimed at startup.
        var keys = items.Where(x => x.State == AttachmentState.Ready).Select(x => x.StorageKey).ToList();
        foreach (var item in items) { MarkForDeletion(item); }
        return keys;
    }

    public Task<PreparedAttachmentFile> PrepareAsync(Stream content, string fileName, string? contentType,
        int? actorUserId, int? cardId = null, DateTime? createdAtUtc = null, CancellationToken cancellationToken = default) =>
        PrepareFileAsync(content, fileName, contentType, actorUserId, cardId, createdAtUtc, options.MaxUploadByteLength, cancellationToken);

    private async Task<PreparedAttachmentFile> PrepareFileAsync(Stream content, string fileName, string? contentType,
        int? actorUserId, int? cardId, DateTime? createdAtUtc, long? maxByteLength, CancellationToken cancellationToken)
    {
        var record = new EntityCardAttachment
        {
            State = AttachmentState.Pending,
            StorageKey = Guid.NewGuid().ToString("N"),
            OriginalFileName = AttachmentFileMetadata.FileName(fileName),
            ContentType = AttachmentFileMetadata.ContentType(contentType),
            CreatedByUserId = actorUserId,
            CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow,
            CardId = cardId
        };
        record.NormalisedFileName = record.OriginalFileName.ToUpperInvariant();
        using (scopes.SuppressAmbientContext())
        using (var scope = scopes.CreateWithTransaction(System.Data.IsolationLevel.Serializable))
        {
            if (cardId.HasValue && await attachments.Query().AnyAsync(x => x.CardId == cardId && x.NormalisedFileName == record.NormalisedFileName, cancellationToken))
            {
                throw new AttachmentNameConflictException(record.OriginalFileName);
            }
            attachments.Add(record);
            await scope.SaveChangesAsync(cancellationToken);
        }

        var prepared = new PreparedAttachmentFile(record.Id, record.StorageKey);
        try
        {
            await using var target = storage.Create(record.StorageKey);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long length = 0;
            int count;
            while ((count = await content.ReadAsync(buffer, cancellationToken)) != 0)
            {
                length += count;
                if (maxByteLength.HasValue && length > maxByteLength.Value) { throw new AttachmentSizeException(maxByteLength.Value); }
                hash.AppendData(buffer, 0, count);
                await target.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            }
            await target.FlushAsync(cancellationToken);
            prepared.ByteLength = length;
            prepared.Sha256 = Convert.ToHexStringLower(hash.GetHashAndReset());
            return prepared;
        }
        catch
        {
            // Release the name after the writer closes; retain the record/file for startup.
            await AbandonAsync(record.Id);
            throw;
        }
    }

    // Caller assigns the final owner and commits in the same transaction.
    public EntityCardAttachment Publish(PreparedAttachmentFile file)
    {
        var record = attachments.Get(file.Id);
        if (record is null || record.State != AttachmentState.Pending) { throw new AttachmentOwnerChangedException(); }
        record.State = AttachmentState.Ready;
        record.ByteLength = file.ByteLength;
        record.Sha256 = file.Sha256;
        return record;
    }

    // Participates in the caller's transaction. Retain the row until its file is gone.
    public static void MarkForDeletion(EntityCardAttachment record)
    {
        record.State = AttachmentState.PendingDeletion;
        record.Card = null;
        record.CardId = null;
        record.ArchivedCard = null;
        record.ArchivedCardId = null;
    }

    public async Task AbandonAsync(int id)
    {
        using var suppressed = scopes.SuppressAmbientContext();
        using var scope = scopes.Create();
        var record = attachments.Get(id);
        if (record is null || record.State == AttachmentState.Ready) { return; }
        MarkForDeletion(record);
        await scope.SaveChangesAsync();
    }

    // Only call before serving requests. No other instance may be using this storage.
    public async Task<int> CleanupAtStartupAsync(CancellationToken cancellationToken = default)
    {
        using var suppressed = scopes.SuppressAmbientContext();
        List<string> keys;
        using (var scope = scopes.Create())
        {
            var records = await attachments.Query().Where(x => x.State != AttachmentState.Ready).ToListAsync(cancellationToken);
            foreach (var record in records) { MarkForDeletion(record); }
            await scope.SaveChangesAsync(cancellationToken);
            keys = records.Select(x => x.StorageKey).ToList();
        }
        return await DeleteFilesAsync(keys, cancellationToken);
    }

    // Normal deletion targets only records committed by that operation.
    // Pending writers are excluded by the caller; startup reclaims those paths.
    public async Task<int> DeleteFilesAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
    {
        using var suppressed = scopes.SuppressAmbientContext();
        var deleted = 0;
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var scope = scopes.Create();
            var record = await attachments.Query().SingleOrDefaultAsync(
                x => x.StorageKey == key && x.State == AttachmentState.PendingDeletion, cancellationToken);
            if (record is null) { continue; }
            try { storage.Delete(key); }
            catch (DirectoryNotFoundException) { /* No file remains to remove. */ }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                record.LastError = exception.Message;
                await scope.SaveChangesAsync(cancellationToken);
                logger?.LogError(exception, "Could not delete attachment {AttachmentId}; retained for next startup.", record.Id);
                continue;
            }
            attachments.Remove(record);
            await scope.SaveChangesAsync(cancellationToken);
            deleted++;
        }
        return deleted;
    }

    public async Task<CardAttachmentCopy> PrepareCopyAsync(int boardId, int cardId, int actorUserId, CancellationToken cancellationToken)
    {
        // Creation may already have an ambient scope containing the validated draft.
        using var suppressed = scopes.SuppressAmbientContext();
        EntityBoardCard source;
        List<EntityCardAttachment> originals;
        using (var scope = scopes.CreateReadOnly())
        {
            if (!await authorisation.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardAccess)
                || !await authorisation.HasPermissionAsync(boardId, actorUserId, BoardPermission.CardCreate))
            {
                throw new UnauthorizedAccessException("You do not have permission to duplicate this card.");
            }
            source = await cards.GetWithTagsAndBoardAsync(boardId, cardId) ?? throw new AttachmentOwnerChangedException();
            originals = await attachments.Query().Where(x => x.CardId == source.Id && x.State == AttachmentState.Ready)
                .AsNoTracking().OrderBy(x => x.Id).ToListAsync(cancellationToken);
        }
        var preparedFiles = new List<PreparedAttachmentFile>();
        foreach (var original in originals)
        {
            await using var input = storage.OpenRead(original.StorageKey);
            // Existing files are not subject to the admission limit for new uploads.
            var prepared = await PrepareFileAsync(input, original.OriginalFileName, original.ContentType, actorUserId,
                null, null, null, cancellationToken);
            preparedFiles.Add(prepared);
            if (prepared.Sha256 != original.Sha256 || prepared.ByteLength != original.ByteLength)
            {
                throw new InvalidDataException("An attachment failed its integrity check.");
            }
        }
        return new CardAttachmentCopy(source.Id, boardId, cardId, originals.Select(x => x.Id).ToList(), preparedFiles);
    }

    // Called inside the card creation transaction, after its first write lock.
    public async Task AttachCopyAsync(CardAttachmentCopy copy, EntityBoardCard target, int actorUserId)
    {
        if (!await authorisation.HasPermissionAsync(copy.BoardId, actorUserId, BoardPermission.BoardAccess)
            || !await authorisation.HasPermissionAsync(copy.BoardId, actorUserId, BoardPermission.CardCreate)
            || !await cards.Query().AnyAsync(x => x.Id == copy.SourceId && x.BoardId == copy.BoardId && x.BoardCardId == copy.CardNumber))
        {
            throw new AttachmentOwnerChangedException();
        }
        var currentIds = await attachments.Query().Where(x => x.CardId == copy.SourceId && x.State == AttachmentState.Ready)
            .OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();
        if (!currentIds.SequenceEqual(copy.OriginalAttachmentIds)) { throw new AttachmentOwnerChangedException(); }
        foreach (var prepared in copy.Files)
        {
            var attachment = Publish(prepared);
            attachment.Card = target;
        }
    }

    public static CardAttachmentDto ToDto(EntityCardAttachment attachment) =>
        new(attachment.Id, attachment.OriginalFileName, attachment.ContentType, attachment.ByteLength,
            attachment.CreatedAtUtc, attachment.CreatedByUserId);

}

public sealed class AttachmentOwnerChangedException : Exception;

public sealed class PreparedAttachmentFile(int id, string storageKey)
{
    public int Id { get; } = id;
    public string StorageKey { get; } = storageKey;
    public long ByteLength { get; internal set; }
    public string Sha256 { get; internal set; } = string.Empty;
}

public sealed class AttachmentSizeException(long limit) : Exception($"Attachment must be {limit} bytes or smaller.");

public sealed class AttachmentNameConflictException(string fileName) : Exception($"An attachment named '{fileName}' already exists on this card.");
