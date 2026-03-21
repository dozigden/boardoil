namespace BoardOil.Ef.Entities;

public sealed class CardTag
{
    public int CardId { get; set; }
    public string TagName { get; set; } = string.Empty;

    public BoardCard Card { get; set; } = null!;
}
