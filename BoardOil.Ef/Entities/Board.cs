namespace BoardOil.Ef.Entities;

public sealed class Board
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<BoardColumn> Columns { get; set; } = new List<BoardColumn>();
}
