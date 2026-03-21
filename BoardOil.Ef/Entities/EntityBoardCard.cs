namespace BoardOil.Ef.Entities;

public sealed class EntityBoardCard
{
    public int Id { get; set; }
    public int BoardColumnId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SortKey { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public EntityBoardColumn BoardColumn { get; set; } = null!;
    public ICollection<EntityCardTag> CardTags { get; set; } = new List<EntityCardTag>();
}
