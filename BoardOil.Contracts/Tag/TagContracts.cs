namespace BoardOil.Contracts.Tag;

public sealed record TagDto(
    int Id,
    string Name,
    string StyleName,
    string StylePropertiesJson,
    string? Emoji,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record UpdateTagStyleRequest(
    string StyleName,
    string StylePropertiesJson,
    string? Emoji = null);

public sealed record CreateTagRequest(
    string Name,
    string? Emoji = null);

public sealed record TagRecord(
    string Name,
    string NormalisedName,
    string StyleName,
    string StylePropertiesJson,
    string? Emoji,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CreateTagRecord(
    string Name,
    string NormalisedName,
    string StyleName,
    string StylePropertiesJson,
    string? Emoji,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record UpdateTagRecord(
    string Name,
    string NormalisedName,
    string StyleName,
    string StylePropertiesJson,
    string? Emoji,
    DateTime UpdatedAtUtc);
