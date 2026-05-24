namespace BoardOil.Data.Abstractions.Entities;

public sealed class EntitySystemInfoMessage : ISupportCreatedAt, ISupportUpdatedAt
{
    public int Id { get; set; }
    public bool Enabled { get; set; }
    public string? Emoji { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string StyleName { get; set; } = string.Empty;
    public string StylePropertiesJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; internal set; }
}
