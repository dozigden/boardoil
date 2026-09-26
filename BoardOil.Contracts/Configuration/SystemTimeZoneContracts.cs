namespace BoardOil.Contracts.Configuration;

public sealed record SystemTimeZoneDto(string SystemTimeZoneId);
public sealed record UpdateSystemTimeZoneRequest(string SystemTimeZoneId);
public sealed record SystemTimeZoneOptionDto(string Id, string DisplayName);
public sealed record SystemTimeZoneOptionsDto(
    string DefaultId, IReadOnlyList<SystemTimeZoneOptionDto> Options);
