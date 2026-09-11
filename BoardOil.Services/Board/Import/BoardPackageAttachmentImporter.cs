using System.IO.Compression;
using BoardOil.Contracts.Board;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Attachment;

namespace BoardOil.Services.Board.Import;

public sealed class BoardPackageAttachmentImporter(CardAttachmentService attachments, ImportedUserResolver users)
{
    public async Task<PreparedBoardAttachments> PrepareAsync(Stream package, BoardPackageAttachmentsDto? manifest,
        BoardPackageImportPlan plan, CancellationToken cancellationToken)
    {
        var items = new List<(BoardPackageAttachmentDto Metadata, PreparedAttachmentFile File)>();
        var prepared = new PreparedBoardAttachments(items);
        if (manifest is null) { return prepared; }
        using var archive = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
        var liveIds = plan.Columns.SelectMany(x => x.Cards).Select(x => x.BoardCardId).ToHashSet();
        var archivedIds = plan.ArchivedCards.Select(x => x.OriginalCardId).ToHashSet();
        var usedNames = new HashSet<(bool Archived, int CardId, string Name)>();
        var usedPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in manifest.Items)
        {
            if (item is null || item.Path is null || !item.Path.StartsWith("files/", StringComparison.Ordinal)
                || item.Path.Length != 38 || !item.Path[6..].All(char.IsAsciiHexDigit) || !usedPaths.Add(item.Path)
                || !(item.Archived ? archivedIds : liveIds).Contains(item.CardId)
                || item.ByteLength < 0 || item.CreatedAtUtc == default
                || item.Sha256 is null || item.Sha256.Length != 64 || !item.Sha256.All(char.IsAsciiHexDigit)
                || AttachmentFileMetadata.FileName(item.OriginalFileName) != item.OriginalFileName)
            {
                throw new InvalidDataException("Attachment metadata or ownership is invalid.");
            }
            if (!usedNames.Add((item.Archived, item.CardId, item.OriginalFileName.ToUpperInvariant())))
            {
                throw new InvalidDataException("Attachment filenames must be unique within each card (ignoring case).");
            }
            var entry = archive.GetEntry(item.Path) ?? throw new InvalidDataException("An attachment file is missing.");
            if (entry.Length != item.ByteLength) { throw new InvalidDataException("Attachment length does not match its metadata."); }
            await using var input = entry.Open();
            var file = await attachments.PrepareAsync(input, item.OriginalFileName, item.ContentType, null,
                createdAtUtc: item.CreatedAtUtc, cancellationToken: cancellationToken);
            items.Add((item, file));
            if (file.ByteLength != item.ByteLength || !file.Sha256.Equals(item.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Attachment checksum does not match its metadata.");
            }
        }
        if (archive.Entries.Any(x => x.FullName.StartsWith("files/", StringComparison.Ordinal) && !usedPaths.Contains(x.FullName)))
        {
            throw new InvalidDataException("Board package contains an unreferenced attachment file.");
        }
        return prepared;
    }
    // Called by the writer inside its transaction; prepared data has no service dependencies.
    public async Task AttachAsync(PreparedBoardAttachments prepared, EntityBoardCard? card, EntityArchivedCard? archivedCard)
    {
        var number = card?.BoardCardId ?? archivedCard!.OriginalCardId;
        foreach (var (metadata, file) in prepared.Items.Where(x => x.Metadata.CardId == number && x.Metadata.Archived == (card is null)))
        {
            var email = metadata.CreatedByUserEmail?.Trim().ToLowerInvariant();
            var user = await users.ResolveImportedCommentAuthorAsync(email);
            var attachment = attachments.Publish(file);
            attachment.CreatedByUserId = user?.Id;
            attachment.Card = card;
            attachment.ArchivedCard = archivedCard;
        }
    }
}

public sealed record PreparedBoardAttachments(IReadOnlyList<(BoardPackageAttachmentDto Metadata, PreparedAttachmentFile File)> Items);
