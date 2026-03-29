namespace BoardOil.Persistence.Abstractions.Entities;

public sealed class EntityCardTag
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public int TagId { get; set; }

    public EntityBoardCard Card { get; set; } = null!;
    public EntityTag Tag { get; set; } = null!;
}
