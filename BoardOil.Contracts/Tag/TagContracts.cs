namespace BoardOil.Contracts.Tag;

public sealed record TagDto(
    int Id,
    string Name,
    string StyleName,
    string StylePropertiesJson,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record UpdateTagStyleRequest(
    string StyleName,
    string StylePropertiesJson);

public sealed record CreateTagRequest(
    string Name);

public sealed record TagRecord(
    string Name,
    string NormalisedName,
    string StyleName,
    string StylePropertiesJson,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CreateTagRecord(
    string Name,
    string NormalisedName,
    string StyleName,
    string StylePropertiesJson,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record UpdateTagRecord(
    string Name,
    string NormalisedName,
    string StyleName,
    string StylePropertiesJson,
    DateTime UpdatedAtUtc);
