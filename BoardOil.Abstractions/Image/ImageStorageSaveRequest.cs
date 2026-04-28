namespace BoardOil.Abstractions.Image;

public sealed class ImageStorageSaveRequest
{
    public ImageStorageEntityType EntityType { get; init; }
    public int EntityId { get; init; }
    public string OriginalFileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public Stream Content { get; init; } = Stream.Null;
}
