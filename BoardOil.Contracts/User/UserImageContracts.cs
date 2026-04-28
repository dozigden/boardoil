namespace BoardOil.Contracts.Users;

public sealed record UserProfileImageDto(
    int Id,
    string ContentType,
    string RelativePath,
    long ByteLength,
    int Width,
    int Height,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
