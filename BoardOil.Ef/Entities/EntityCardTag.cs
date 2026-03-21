namespace BoardOil.Ef.Entities;

public sealed class EntityCardTag
{
    public int CardId { get; set; }
    public string TagName { get; set; } = string.Empty;

    public EntityBoardCard Card { get; set; } = null!;
}
