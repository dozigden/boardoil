namespace BoardOil.Data.Abstractions.Entities;

public sealed class EntitySlick : ISupportCreatedAt, ISupportUpdatedAt
{
    public int Id { get; set; }
    public int BoardId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalisedName { get; set; } = string.Empty;
    public string StyleName { get; set; } = string.Empty;
    public string StylePropertiesJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; internal set; }

    public EntityBoard Board { get; set; } = null!;
    public ICollection<EntityBoardCard> Cards { get; set; } = new List<EntityBoardCard>();
}
