namespace BoardOil.Ef.Entities;

public sealed class BoardColumn
{
    public int Id { get; set; }
    public int BoardId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Position { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Board Board { get; set; } = null!;
    public ICollection<BoardCard> Cards { get; set; } = new List<BoardCard>();
}
