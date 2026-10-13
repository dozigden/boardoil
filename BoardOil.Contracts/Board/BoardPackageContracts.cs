using System.Text.Json;
using BoardOil.Contracts.CustomField;

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
    IReadOnlyList<BoardPackageColumnDto> Columns,
    IReadOnlyList<BoardPackageSlickDto>? Slicks = null,
    bool SlickCohesionModeEnabled = true,
    bool CardAttachmentThumbnailsEnabled = true)
{
    public IReadOnlyList<BoardPackageCustomField> CustomFields { get; init; } = [];
}

public sealed record BoardPackageCustomFieldApplicability(string Mode, IReadOnlyList<string>? CardTypeNames = null);
public sealed record BoardPackageCustomField(CustomFieldDefinition Definition, BoardPackageCustomFieldApplicability AppliesTo);

public sealed record BoardPackageCardTypeDto(
    string Name,
    string? Emoji,
    bool IsSystem,
    string? StyleName = null,
    string? StylePropertiesJson = null,
    int? Id = null);

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
    string? AssignedUserEmail = null,
    IReadOnlyList<BoardPackageCommentDto>? Comments = null,
    string? SlickName = null,
    string? ExternalUrl = null,
    int? Id = null,
    DateTime? CardCreatedUtc = null,
    DateTime? CardUpdatedUtc = null)
{
    public IReadOnlyDictionary<string, JsonElement> CustomFieldValues { get; init; } = new Dictionary<string, JsonElement>();
}

public sealed record BoardPackageSlickDto(
    string Name,
    string StyleName,
    string StylePropertiesJson);

public sealed record BoardPackageCommentDto(
    string Text,
    DateTime PostedAtUtc,
    string? AuthorEmail = null,
    DateTime? EditedAtUtc = null,
    bool IsSystem = false);

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
    Stream Content);

public sealed record BoardPackageAttachmentDto(string Path, int CardId, bool Archived, string OriginalFileName,
    string ContentType, long ByteLength, string Sha256, DateTime CreatedAtUtc, string? CreatedByUserEmail);

public sealed record BoardPackageAttachmentsDto(IReadOnlyList<BoardPackageAttachmentDto> Items);
