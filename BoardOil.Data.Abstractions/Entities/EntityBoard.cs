namespace BoardOil.Data.Abstractions.Entities;

public sealed class EntityBoard : ISupportCreatedAt, ISupportUpdatedAt
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; internal set; }

    public ICollection<EntityBoardColumn> Columns { get; set; } = new List<EntityBoardColumn>();
    public ICollection<EntityCardType> CardTypes { get; set; } = new List<EntityCardType>();
    public ICollection<EntityArchivedCard> ArchivedCards { get; set; } = new List<EntityArchivedCard>();
    public ICollection<EntityTag> Tags { get; set; } = new List<EntityTag>();
    public ICollection<EntityBoardMember> Members { get; set; } = new List<EntityBoardMember>();
}
