using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using BoardOil.Abstractions.Board;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Contracts;
using BoardOil.Persistence.Abstractions.Board;
using BoardOil.Persistence.Abstractions.Card;
using BoardOil.Persistence.Abstractions.CardType;
using BoardOil.Persistence.Abstractions.Column;
using BoardOil.Persistence.Abstractions.Tag;
using BoardOil.Services.Card;

namespace BoardOil.Services.Board;

public sealed class BoardExportService(
    IBoardRepository boardRepository,
    IColumnRepository columnRepository,
    ICardRepository cardRepository,
    IArchivedCardRepository archivedCardRepository,
    ICardTypeRepository cardTypeRepository,
    ITagRepository tagRepository,
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
        var archivedCards = await archivedCardRepository.ListForExportAsync(boardId);
        var cardTypes = await cardTypeRepository.GetAllForBoardAsync(boardId);
        var tags = await tagRepository.GetAllForBoardAsync(boardId);

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
                            .ToList()))
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
                .ToList());
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
