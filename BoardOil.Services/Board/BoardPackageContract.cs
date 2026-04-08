using BoardOil.Contracts.Board;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Services.Board;

public static class BoardPackageContract
{
    public const string PackageFormat = "boardoil-board-package";
    public const int CurrentSchemaVersion = 1;
    public const string ManifestPath = "manifest.json";
    public const string BoardEntryKind = "board";
    public const string BoardEntryPath = "board.json";

    public static BoardPackageManifestDto CreateManifest(string exportedByVersion)
    {
        var normalisedExporterVersion = string.IsNullOrWhiteSpace(exportedByVersion)
            ? "unknown"
            : exportedByVersion.Trim();

        return new BoardPackageManifestDto(
            PackageFormat,
            CurrentSchemaVersion,
            normalisedExporterVersion,
            [new BoardPackageManifestEntryDto(BoardEntryKind, BoardEntryPath)]);
    }

    public static bool IsSupportedSchemaVersion(int schemaVersion) =>
        schemaVersion == CurrentSchemaVersion;

    public static ApiError? ValidateManifest(BoardPackageManifestDto manifest)
    {
        var errors = new List<ValidationError>();

        if (!string.Equals(manifest.Format?.Trim(), PackageFormat, StringComparison.Ordinal))
        {
            errors.Add(new ValidationError("manifest.format", $"Manifest format must be '{PackageFormat}'."));
        }

        if (manifest.SchemaVersion <= 0)
        {
            errors.Add(new ValidationError("manifest.schemaVersion", "Schema version must be greater than zero."));
        }
        else if (manifest.SchemaVersion > CurrentSchemaVersion)
        {
            errors.Add(new ValidationError(
                "manifest.schemaVersion",
                $"Schema version '{manifest.SchemaVersion}' is not supported by this importer. Maximum supported version is '{CurrentSchemaVersion}'."));
        }

        if (string.IsNullOrWhiteSpace(manifest.ExportedByVersion))
        {
            errors.Add(new ValidationError("manifest.exportedByVersion", "Exporter version is required."));
        }

        var boardEntries = manifest.Entries
            .Where(x => string.Equals(x.Kind?.Trim(), BoardEntryKind, StringComparison.Ordinal))
            .ToList();

        if (boardEntries.Count != 1)
        {
            errors.Add(new ValidationError("manifest.entries", $"Manifest must contain exactly one '{BoardEntryKind}' entry."));
        }
        else if (!string.Equals(boardEntries[0].Path?.Trim(), BoardEntryPath, StringComparison.Ordinal))
        {
            errors.Add(new ValidationError(
                "manifest.entries",
                $"'{BoardEntryKind}' entry path must be '{BoardEntryPath}'."));
        }

        return errors.Count == 0
            ? null
            : ApiErrors.BadRequest("Board package manifest is invalid.", errors);
    }
}
