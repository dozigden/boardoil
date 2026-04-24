namespace BoardOil.Contracts.Board;

public sealed record BoardPackageManifestDto(
    string Format,
    int SchemaVersion,
    string ExportedByVersion,
    IReadOnlyList<BoardPackageManifestEntryDto> Entries);

public sealed record BoardPackageManifestEntryDto(
    string Kind,
    string Path);

public sealed record BoardPackageBoardDto(
    string Name,
    string? Description,
    IReadOnlyList<BoardPackageCardTypeDto> CardTypes,
    IReadOnlyList<BoardPackageTagDto> Tags,
    IReadOnlyList<BoardPackageColumnDto> Columns);

public sealed record BoardPackageCardTypeDto(
    string Name,
    string? Emoji,
    bool IsSystem,
    string? StyleName = null,
    string? StylePropertiesJson = null);

public sealed record BoardPackageTagDto(
    string Name,
    string StyleName,
    string StylePropertiesJson,
    string? Emoji);

public sealed record BoardPackageColumnDto(
    string Title,
    IReadOnlyList<BoardPackageCardDto> Cards);

public sealed record BoardPackageCardDto(
    string Title,
    string Description,
    string CardTypeName,
    IReadOnlyList<string> TagNames,
    string? AssignedUserEmail = null);

public sealed record BoardPackageArchiveDto(
    IReadOnlyList<BoardPackageArchivedCardDto> Cards);

public sealed record BoardPackageArchivedCardDto(
    int OriginalCardId,
    string Title,
    IReadOnlyList<string> TagNames,
    DateTime ArchivedAtUtc,
    string SnapshotJson);

public sealed record BoardPackageExportDto(
    string FileName,
    string ContentType,
    byte[] Content);
