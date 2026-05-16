namespace BoardOil.Services.Board.Import;

public static class BoardPackageImportLimits
{
    public const int MaxBoardNameLength = 120;
    public const int MaxBoardDescriptionLength = 5_000;
    public const int MaxColumnNameLength = 200;
    public const int MaxCardTitleLength = 200;
    public const int MaxCardDescriptionLength = 20_000;
    public const int MaxCardCommentLength = 4_000;
    public const int MaxTagNameLength = 40;
    public const int MaxCardTypeNameLength = 40;
    public const int MaxArchiveTitleLength = 200;
    public const int MaxArchiveSnapshotJsonBytes = 2_097_152;
    public const int MaxArchiveSearchTagsJsonLength = 65_535;
    public const int MaxArchiveSearchTextNormalisedLength = 65_535;
}
