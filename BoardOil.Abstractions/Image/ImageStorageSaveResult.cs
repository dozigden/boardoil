namespace BoardOil.Abstractions.Image;

public sealed class ImageStorageSaveResult
{
    public string RelativePath { get; init; } = string.Empty;
    public long ByteLength { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
