namespace BoardOil.Data.Abstractions.Entities;

public sealed class EntityCardComment : ISupportCreatedAt
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public int? AuthorUserId { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime PostedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public EntityBoardCard Card { get; set; } = null!;
    public EntityUser? AuthorUser { get; set; }
}
