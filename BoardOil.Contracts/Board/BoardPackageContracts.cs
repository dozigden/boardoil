namespace BoardOil.Contracts.Board;

public sealed record BoardPackageManifestDto(
    string Format,
    int SchemaVersion,
    string ExportedByVersion,
    IReadOnlyList<BoardPackageManifestEntryDto> Entries);

public sealed record BoardPackageManifestEntryDto(
    string Kind,
    string Path);
