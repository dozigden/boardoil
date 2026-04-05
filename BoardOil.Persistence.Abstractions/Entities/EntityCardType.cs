namespace BoardOil.Persistence.Abstractions.Entities;

public sealed class EntityCardType
{
    public int Id { get; set; }
    public int BoardId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Emoji { get; set; }
    public bool IsSystem { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public EntityBoard Board { get; set; } = null!;
    public ICollection<EntityBoardCard> Cards { get; set; } = new List<EntityBoardCard>();
}
