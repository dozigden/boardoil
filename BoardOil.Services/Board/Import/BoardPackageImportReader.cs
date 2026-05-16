using System.IO.Compression;
using System.Text.Json;
using BoardOil.Contracts.Board;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Services.Board.Import;

public sealed class BoardPackageImportReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public BoardPackageReadResult TryReadBoardPackage(byte[] packageContent)
    {
        try
        {
            using var stream = new MemoryStream(packageContent);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            var manifestEntry = archive.GetEntry(BoardPackageContract.ManifestPath);
            if (manifestEntry is null)
            {
                return new BoardPackageReadResult(
                    null,
                    null,
                    ApiErrors.ValidationFailed([new ValidationError("manifest", $"Board package is missing '{BoardPackageContract.ManifestPath}'.")]));
            }

            BoardPackageManifestDto? manifest;
            using (var manifestReader = new StreamReader(manifestEntry.Open()))
            {
                var manifestJson = manifestReader.ReadToEnd();
                manifest = JsonSerializer.Deserialize<BoardPackageManifestDto>(manifestJson, JsonOptions);
            }

            if (manifest is null)
            {
                return new BoardPackageReadResult(
                    null,
                    null,
                    ApiErrors.ValidationFailed([new ValidationError("manifest", "Board package manifest is invalid JSON.")]));
            }

            if (manifest.Entries is null)
            {
                return new BoardPackageReadResult(
                    null,
                    null,
                    ApiErrors.ValidationFailed([new ValidationError("manifest.entries", "Manifest entries are required.")]));
            }

            var manifestValidationError = BoardPackageContract.ValidateManifest(manifest);
            if (manifestValidationError is not null)
            {
                return new BoardPackageReadResult(null, null, manifestValidationError);
            }

            var boardEntry = archive.GetEntry(BoardPackageContract.BoardEntryPath);
            if (boardEntry is null)
            {
                return new BoardPackageReadResult(
                    null,
                    null,
                    ApiErrors.ValidationFailed([new ValidationError("board", $"Board package is missing '{BoardPackageContract.BoardEntryPath}'.")]));
            }

            BoardPackageBoardDto? boardPayload;
            using (var boardReader = new StreamReader(boardEntry.Open()))
            {
                var boardJson = boardReader.ReadToEnd();
                var parseBoardPayloadResult = TryParseBoardPayload(manifest.SchemaVersion, boardJson);
                if (parseBoardPayloadResult.Error is not null)
                {
                    return new BoardPackageReadResult(null, null, parseBoardPayloadResult.Error);
                }

                boardPayload = parseBoardPayloadResult.BoardPayload;
            }

            if (boardPayload is null)
            {
                return new BoardPackageReadResult(
                    null,
                    null,
                    ApiErrors.ValidationFailed([new ValidationError("board", "Board payload is invalid JSON.")]));
            }

            BoardPackageArchiveDto? archivePayload = null;
            var hasArchiveEntry = manifest.Entries.Any(x =>
                string.Equals(x.Kind?.Trim(), BoardPackageContract.ArchiveEntryKind, StringComparison.Ordinal)
                && string.Equals(x.Path?.Trim(), BoardPackageContract.ArchiveEntryPath, StringComparison.Ordinal));
            if (hasArchiveEntry)
            {
                var archiveEntry = archive.GetEntry(BoardPackageContract.ArchiveEntryPath);
                if (archiveEntry is null)
                {
                    return new BoardPackageReadResult(
                        null,
                        null,
                        ApiErrors.ValidationFailed([new ValidationError("archive", $"Board package is missing '{BoardPackageContract.ArchiveEntryPath}'.")]));
                }

                using var archiveReader = new StreamReader(archiveEntry.Open());
                var archiveJson = archiveReader.ReadToEnd();
                var parseArchivePayloadResult = TryParseArchivePayload(manifest.SchemaVersion, archiveJson);
                if (parseArchivePayloadResult.Error is not null)
                {
                    return new BoardPackageReadResult(
                        null,
                        null,
                        parseArchivePayloadResult.Error);
                }

                archivePayload = parseArchivePayloadResult.ArchivePayload;
            }

            return new BoardPackageReadResult(boardPayload, archivePayload, null);
        }
        catch (InvalidDataException)
        {
            return new BoardPackageReadResult(
                null,
                null,
                ApiErrors.ValidationFailed([new ValidationError("file", "Uploaded file is not a valid ZIP archive.")]));
        }
        catch (JsonException)
        {
            return new BoardPackageReadResult(
                null,
                null,
                ApiErrors.ValidationFailed([new ValidationError("file", "Board package JSON content is invalid.")]));
        }
    }

    private static ParseBoardPayloadResult TryParseBoardPayload(int schemaVersion, string boardJson)
    {
        switch (schemaVersion)
        {
            case 1:
            case 2:
            {
                var boardPayload = JsonSerializer.Deserialize<BoardPackageBoardDto>(boardJson, JsonOptions);
                return new ParseBoardPayloadResult(boardPayload, null);
            }
            default:
                return new ParseBoardPayloadResult(
                    null,
                    ApiErrors.ValidationFailed([new ValidationError(
                        "manifest.schemaVersion",
                        $"Schema version '{schemaVersion}' does not have an import payload handler configured.")]));
        }
    }

    private static ParseArchivePayloadResult TryParseArchivePayload(int schemaVersion, string archiveJson)
    {
        switch (schemaVersion)
        {
            case 1:
            case 2:
            {
                var archivePayload = JsonSerializer.Deserialize<BoardPackageArchiveDto>(archiveJson, JsonOptions);
                if (archivePayload is null)
                {
                    return new ParseArchivePayloadResult(
                        null,
                        ApiErrors.ValidationFailed([new ValidationError("archive", "Archive payload is invalid JSON.")]));
                }

                return new ParseArchivePayloadResult(archivePayload, null);
            }
            default:
                return new ParseArchivePayloadResult(
                    null,
                    ApiErrors.ValidationFailed([new ValidationError(
                        "manifest.schemaVersion",
                        $"Schema version '{schemaVersion}' does not have an archive payload handler configured.")]));
        }
    }

    private sealed record ParseBoardPayloadResult(
        BoardPackageBoardDto? BoardPayload,
        ApiError? Error);

    private sealed record ParseArchivePayloadResult(
        BoardPackageArchiveDto? ArchivePayload,
        ApiError? Error);
}
