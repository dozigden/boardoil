namespace BoardOil.Contracts.Slick;

public sealed record SlickDto(
    int Id,
    string Name,
    string StyleName,
    string StylePropertiesJson,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CreateSlickRequest(
    string Name,
    string? StyleName = null,
    string? StylePropertiesJson = null);

public sealed record UpdateSlickRequest(
    string Name,
    string StyleName,
    string StylePropertiesJson);
