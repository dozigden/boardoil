namespace BoardOil.TasksMd;

public sealed record TasksMdBoardImportModel(
    IReadOnlyList<TasksMdImportedColumn> Columns,
    IReadOnlyList<TasksMdImportedTag> Tags);

public sealed record TasksMdImportedColumn(
    string Name,
    IReadOnlyList<TasksMdImportedCard> Cards);

public sealed record TasksMdImportedCard(
    string Name,
    string Description,
    IReadOnlyList<string> TagNames);

public sealed record TasksMdImportedTag(
    string Name,
    string? HexColor);
