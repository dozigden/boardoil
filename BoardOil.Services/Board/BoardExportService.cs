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
    IDbContextScopeFactory scopeFactory) : IBoardExportService
{
    private const string ZipContentType = "application/zip";
    private static readonly Regex InvalidFileNameCharactersRegex = new($"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]+", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<ApiResult<BoardPackageExportDto>> ExportBoardAsync(int boardId, int actorUserId, string exportedByVersion)
    {
        using var scope = scopeFactory.CreateReadOnly();

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
                        card.ExternalUrl))
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
            board.SlickCohesionModeEnabled);
        var archivePayload = new BoardPackageArchiveDto(
            archivedCards
                .Select(x => x.ToArchivedCardDto())
                .Select(x => new BoardPackageArchivedCardDto(
                    x.OriginalCardId,
                    x.Title,
                    x.TagNames,
                    x.ArchivedAtUtc,
                    x.SnapshotJson))
                .ToList());

        var manifest = BoardPackageContract.CreateManifest(exportedByVersion);
        var packageBytes = BuildPackage(manifest, boardPayload, archivePayload);
        var fileName = BuildExportFileName(board.Name);

        return ApiResults.Ok(new BoardPackageExportDto(
            fileName,
            ZipContentType,
            packageBytes));
    }

    private static byte[] BuildPackage(BoardPackageManifestDto manifest, BoardPackageBoardDto boardPayload, BoardPackageArchiveDto archivePayload)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteJsonEntry(archive, BoardPackageContract.ManifestPath, manifest);
            WriteJsonEntry(archive, BoardPackageContract.BoardEntryPath, boardPayload);
            WriteJsonEntry(archive, BoardPackageContract.ArchiveEntryPath, archivePayload);
        }

        return stream.ToArray();
    }

    private static void WriteJsonEntry<T>(ZipArchive archive, string entryPath, T payload)
    {
        var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream);
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        writer.Write(json);
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
