namespace BoardOil.Persistence.Abstractions.Entities;

public sealed class EntityBoardMember
{
    public int Id { get; set; }
    public int BoardId { get; set; }
    public int UserId { get; set; }
    public BoardMemberRole Role { get; set; } = BoardMemberRole.Owner;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public EntityBoard Board { get; set; } = null!;
    public EntityUser User { get; set; } = null!;
}
