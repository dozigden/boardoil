namespace BoardOil.Persistence.Abstractions.Entities;

public sealed class EntityTag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalisedName { get; set; } = string.Empty;
    public string StyleName { get; set; } = string.Empty;
    public string StylePropertiesJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<EntityCardTag> CardTags { get; set; } = new List<EntityCardTag>();
}
