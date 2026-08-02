namespace BoardOil.Contracts.Users;

public static class ProfileImageUploadConstraints
{
    public const string ContentType = "image/png";
    public const int Width = 512;
    public const int Height = 512;
    public const int MaxByteLength = 2 * 1024 * 1024;
}

public sealed record UserProfileImageDto(
    int Id,
    string ContentType,
    string RelativePath,
    long ByteLength,
    int Width,
    int Height,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
