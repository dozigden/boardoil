namespace BoardOil.Persistence.Abstractions.Entities;

public sealed class EntityCardTag
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public string TagName { get; set; } = string.Empty;

    public EntityBoardCard Card { get; set; } = null!;
}
