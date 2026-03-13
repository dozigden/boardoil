namespace BoardOil.Ef.Entities;

public sealed class BoardCard
{
    public int Id { get; set; }
    public int BoardColumnId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SortKey { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public BoardColumn BoardColumn { get; set; } = null!;
}
