namespace BoardOil.Persistence.Abstractions.Entities;

public sealed class EntityImage
{
    public int Id { get; set; }
    public ImageEntityType EntityType { get; set; }
    public int EntityId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public long ByteLength { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
