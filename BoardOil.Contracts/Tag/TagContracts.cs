namespace BoardOil.Contracts.Tag;

public sealed record TagDto(
    int Id,
    string Name,
    string StyleName,
    string StylePropertiesJson,
    string? Emoji,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record UpdateTagRequest(
    string Name,
    string StyleName,
    string StylePropertiesJson,
    string? Emoji = null);

public sealed record CreateTagRequest(
    string Name,
    string? Emoji = null);
