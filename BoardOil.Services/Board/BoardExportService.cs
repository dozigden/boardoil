using System.Data;
using BoardOil.Abstractions.Attachment;
using System.Security.Cryptography;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Common;
using BoardOil.Data.Abstractions.Board;
using BoardOil.Data.Abstractions.Card;
using BoardOil.Data.Abstractions.CardType;
using BoardOil.Data.Abstractions.Column;
using BoardOil.Data.Abstractions.Slick;
using BoardOil.Data.Abstractions.Tag;
using BoardOil.Services.Card;
using BoardOil.Data.Abstractions.Attachment;
using BoardOil.Data.Abstractions.Entities;
using BoardOil.Services.Attachment;
using Microsoft.EntityFrameworkCore;

namespace BoardOil.Services.Board;

public sealed class BoardExportService(
    IBoardRepository boardRepository,
    IColumnRepository columnRepository,
    ICardRepository cardRepository,
    ICardCommentRepository cardCommentRepository,
    IArchivedCardRepository archivedCardRepository,
    ICardTypeRepository cardTypeRepository,
    ITagRepository tagRepository,
    ISlickRepository slickRepository,
    IBoardAuthorisationService boardAuthorisationService,
    IDbContextScopeFactory scopeFactory,
    IAttachmentRepository attachments,
    IAttachmentStorageService files,
    BoardPackageStorageService packages) : IBoardExportService
{
    private const string ZipContentType = "application/zip";
    private static readonly Regex InvalidFileNameCharactersRegex = new($"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]+", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<ApiResult<BoardPackageExportDto>> ExportBoardAsync(int boardId, int actorUserId, string exportedByVersion, CancellationToken cancellationToken = default)
    {
        using (var accessScope = scopeFactory.CreateReadOnly())
        {
            if (boardRepository.Get(boardId) is null) { return ApiErrors.NotFound("Board not found."); }
            if (!await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardManageSettings))
            {
                return ApiErrors.Forbidden("You do not have permission for this action.");
            }
        }
        // Track the temporary file before opening the board's consistent read transaction.
        var package = await packages.CreateAsync(cancellationToken);
        try
        {
            var result = await ExportSnapshotAsync(boardId, actorUserId, exportedByVersion, package, cancellationToken);
            if (!result.Success) { await package.DisposeAsync(); }
            return result;
        }
        catch { await package.DisposeAsync(); throw; }
    }

    private async Task<ApiResult<BoardPackageExportDto>> ExportSnapshotAsync(int boardId, int actorUserId, string exportedByVersion,
        FileStream package, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateReadOnlyWithTransaction(IsolationLevel.Serializable);

        var board = boardRepository.Get(boardId);
        if (board is null)
        {
            return ApiErrors.NotFound("Board not found.");
        }

        var hasPermission = await boardAuthorisationService.HasPermissionAsync(boardId, actorUserId, BoardPermission.BoardManageSettings);
        if (!hasPermission)
        {
            return ApiErrors.Forbidden("You do not have permission for this action.");
        }

        var columns = await columnRepository.GetColumnsInBoardOrderedAsync(boardId);
        var columnIds = columns.Select(x => x.Id).ToList();
        var cards = await cardRepository.GetCardsForColumnsOrderedAsync(columnIds);
        var cardIds = cards.Select(x => x.Id).ToList();
        var comments = await cardCommentRepository.GetForCardsOrderedAsync(cardIds);
        var archivedCards = await archivedCardRepository.ListForExportAsync(boardId);
        var cardTypes = await cardTypeRepository.GetAllForBoardAsync(boardId);
        var tags = await tagRepository.GetAllForBoardAsync(boardId);
        var slicks = await slickRepository.GetAllForBoardAsync(boardId);
        var slickNamesById = slicks.ToDictionary(x => x.Id, x => x.Name);
        var commentsByCardId = comments
            .GroupBy(x => x.CardId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<BoardPackageCommentDto>)group
                    .Select(comment => new BoardPackageCommentDto(
                        comment.Text,
                        comment.PostedAtUtc,
                        comment.AuthorUser?.Email))
                    .ToList());

        var cardsByColumnId = cards
            .GroupBy(x => x.BoardColumnId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<BoardPackageCardDto>)x
                    .OrderBy(card => card.SortKey)
                    .Select(card => new BoardPackageCardDto(
                        card.Title,
                        card.Description,
                        card.CardType.Name,
                        card.CardTags
                            .OrderBy(cardTag => cardTag.Tag.Name)
                            .Select(cardTag => cardTag.Tag.Name)
                            .ToList(),
                        card.AssignedUser?.Email,
                        commentsByCardId.GetValueOrDefault(card.Id, []),
                        card.SlickId is null ? null : slickNamesById.GetValueOrDefault(card.SlickId.Value),
                        card.ExternalUrl,
                        card.RequireBoardCardId(),
                        card.CardCreatedUtc,
                        card.CardUpdatedUtc))
                    .ToList());

        var boardPayload = new BoardPackageBoardDto(
            board.Name,
            board.Description,
            cardTypes
                .Select(x => new BoardPackageCardTypeDto(x.Name, x.Emoji, x.IsSystem, x.StyleName, x.StylePropertiesJson))
                .ToList(),
            tags
                .Select(x => new BoardPackageTagDto(x.Name, x.StyleName, x.StylePropertiesJson, x.Emoji))
                .ToList(),
            columns
                .Select(x => new BoardPackageColumnDto(
                    x.Title,
                    cardsByColumnId.GetValueOrDefault(x.Id, [])))
                .ToList(),
            slicks
                .Select(x => new BoardPackageSlickDto(
                    x.Name,
                    x.StyleName,
                    x.StylePropertiesJson))
                .ToList(),
            board.SlickCohesionModeEnabled,
            board.CardAttachmentThumbnailsEnabled);
        var archivePayload = new BoardPackageArchiveDto(
            archivedCards
                .Select(x => x.ToArchivedCardDto())
                .Select(x => new BoardPackageArchivedCardDto(
                    x.Id,
                    x.Title,
                    x.TagNames,
                    x.ArchivedAtUtc,
                    x.SnapshotJson))
                .ToList());

        var manifest = BoardPackageContract.CreateManifest(exportedByVersion);
        var attachmentRecords = await attachments.Query()
            .Where(x => x.State == AttachmentState.Ready && ((x.Card != null && x.Card.BoardId == boardId) || (x.ArchivedCard != null && x.ArchivedCard.BoardId == boardId)))
            .Include(x => x.Card).Include(x => x.ArchivedCard).Include(x => x.CreatedByUser).OrderBy(x => x.Id).ToListAsync();
        Stream packageBytes;
        try { packageBytes = await BuildPackageAsync(manifest, boardPayload, archivePayload, attachmentRecords, package, cancellationToken); }
        catch (InvalidDataException exception) { return ApiErrors.BadRequest(exception.Message); }
        catch (IOException) { return ApiErrors.InternalError("An attachment could not be read. The board package was not exported."); }
        var fileName = BuildExportFileName(board.Name);

        return ApiResults.Ok(new BoardPackageExportDto(
            fileName,
            ZipContentType,
            packageBytes));
    }

    private async Task<Stream> BuildPackageAsync(BoardPackageManifestDto manifest, BoardPackageBoardDto boardPayload,
        BoardPackageArchiveDto archivePayload, IReadOnlyList<EntityCardAttachment> records, FileStream stream, CancellationToken cancellationToken)
    {
        var metadata = new BoardPackageAttachmentsDto(records.Select(x => new BoardPackageAttachmentDto(
            "files/" + Guid.NewGuid().ToString("N"), x.Card?.BoardCardId ?? x.ArchivedCard!.OriginalCardId,
            x.ArchivedCardId.HasValue, x.OriginalFileName, x.ContentType, x.ByteLength, x.Sha256, x.CreatedAtUtc, x.CreatedByUser?.Email)).ToList());
        // ExportBoardAsync owns disposal, after the snapshot transaction has ended.
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteJsonEntry(archive, BoardPackageContract.ManifestPath, manifest);
            WriteJsonEntry(archive, BoardPackageContract.BoardEntryPath, boardPayload);
            WriteJsonEntry(archive, BoardPackageContract.ArchiveEntryPath, archivePayload);
            WriteJsonEntry(archive, BoardPackageContract.AttachmentsEntryPath, metadata);
            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                await using var input = files.OpenRead(record.StorageKey);
                await using var output = archive.CreateEntry(metadata.Items[index].Path, CompressionLevel.Fastest).Open();
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[81920];
                long length = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, cancellationToken)) != 0)
                {
                    length += count;
                    hash.AppendData(buffer, 0, count);
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                }
                if (length != record.ByteLength || Convert.ToHexStringLower(hash.GetHashAndReset()) != record.Sha256)
                {
                    throw new InvalidDataException("An attachment failed its integrity check. The board package was not exported.");
                }
            }
        }
        stream.Position = 0;
        return stream;
    }

    private static void WriteJsonEntry<T>(ZipArchive archive, string entryPath, T payload)
    {
        var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        JsonSerializer.Serialize(entryStream, payload, JsonOptions);
    }

    private static string BuildExportFileName(string boardName)
    {
        var slug = InvalidFileNameCharactersRegex
            .Replace(boardName.Trim(), "-")
            .Replace(' ', '-');

        slug = Regex.Replace(slug, "-{2,}", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "board";
        }

        return $"{slug}.boardoil.zip";
    }
}
