namespace BoardOil.Abstractions.Image;

public sealed class ImageStorageSaveRequest
{
    public ImageStorageEntityType EntityType { get; init; }
    public int EntityId { get; init; }
    public Stream Content { get; init; } = Stream.Null;
}
