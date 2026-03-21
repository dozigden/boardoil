namespace BoardOil.Persistence.Abstractions.Entities;

public sealed class EntityBoardColumn
{
    public int Id { get; set; }
    public int BoardId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SortKey { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public EntityBoard Board { get; set; } = null!;
    public ICollection<EntityBoardCard> Cards { get; set; } = new List<EntityBoardCard>();
}
