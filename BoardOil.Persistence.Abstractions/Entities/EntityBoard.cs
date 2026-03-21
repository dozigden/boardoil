namespace BoardOil.Persistence.Abstractions.Entities;

public sealed class EntityBoard
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<EntityBoardColumn> Columns { get; set; } = new List<EntityBoardColumn>();
}
