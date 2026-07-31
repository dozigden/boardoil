namespace BoardOil.Data.Abstractions.Entities;

public sealed class EntityBoardCardIdSequence
{
    public int Id { get; set; }
    public int BoardId { get; set; }
    public int NextCardId { get; set; } = 1;

    public EntityBoard Board { get; set; } = null!;
}
