using BoardOil.Contracts.Board;
using BoardOil.Contracts.Common;

namespace BoardOil.Services.Board.Import;

public sealed record BoardPackageReadResult(
    BoardPackageBoardDto? BoardPayload,
    BoardPackageArchiveDto? ArchivePayload,
    ApiError? Error,
    int? SchemaVersion = null);

public sealed record BoardPackageImportPlanResult(
    BoardPackageImportPlan? Plan,
    ApiError? Error);

public sealed record BoardPackageImportPlan(
    string BoardName,
    string BoardDescription,
    bool SlickCohesionModeEnabled,
    int NextCardId,
    string SystemCardTypeName,
    string SystemCardTypeNormalisedName,
    string? SystemCardTypeEmoji,
    string SystemCardTypeStyleName,
    string SystemCardTypeStylePropertiesJson,
    IReadOnlyList<CardTypeImportDefinition> CardTypes,
    IReadOnlyList<TagImportDefinition> TagDefinitions,
    IReadOnlyList<SlickImportDefinition> SlickDefinitions,
    IReadOnlyList<ColumnImportDefinition> Columns,
    IReadOnlyList<ArchivedCardImportDefinition> ArchivedCards);

public sealed record CardTypeImportDefinition(
    string Name,
    string NormalisedName,
    string? Emoji,
    string StyleName,
    string StylePropertiesJson);

public sealed record TagImportDefinition(
    string Name,
    string NormalisedName,
    string StyleName,
    string StylePropertiesJson,
    string? Emoji);

public sealed record SlickImportDefinition(
    string Name,
    string NormalisedName,
    string StyleName,
    string StylePropertiesJson);

public sealed record ColumnImportDefinition(
    string Title,
    IReadOnlyList<CardImportDefinition> Cards);

public sealed record CardImportDefinition(
    string Title,
    string Description,
    string CardTypeNormalisedName,
    IReadOnlyList<string> TagNames,
    string? SlickNormalisedName,
    string? AssignedUserNormalisedEmail,
    IReadOnlyList<CommentImportDefinition> Comments,
    string? ExternalUrl = null,
    int BoardCardId = 0);

public sealed record CommentImportDefinition(
    string Text,
    DateTime PostedAtUtc,
    string? AuthorNormalisedEmail);

public sealed record ArchivedCardImportDefinition(
    int OriginalCardId,
    string Title,
    IReadOnlyList<string> TagNames,
    DateTime ArchivedAtUtc,
    string SnapshotJson);

public sealed record TagNameValidationResult(
    string CanonicalName,
    string NormalisedName,
    ValidationError? Error);

public sealed record CardTypeNameValidationResult(
    string CanonicalName,
    string NormalisedName,
    ValidationError? Error);

public sealed record CardTypeStyleResolution(
    string StyleName,
    string StylePropertiesJson,
    ValidationError? Error);

public sealed record SlickNameValidationResult(
    string CanonicalName,
    string NormalisedName,
    ValidationError? Error);

public sealed record SlickStyleResolution(
    string StyleName,
    string StylePropertiesJson,
    ValidationError? Error);
