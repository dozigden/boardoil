namespace BoardOil.Data.Abstractions.Entities;

public sealed class EntityBoardCard : ISupportCreatedAt, ISupportUpdatedAt
{
    public int Id { get; set; }
    public int BoardColumnId { get; set; }
    public int CardTypeId { get; set; }
    public int? AssignedUserId { get; set; }
    public int? SlickId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SortKey { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; internal set; }

    public EntityBoardColumn BoardColumn { get; set; } = null!;
    public EntityCardType CardType { get; set; } = null!;
    public EntityUser? AssignedUser { get; set; }
    public EntitySlick? Slick { get; set; }
    public ICollection<EntityCardTag> CardTags { get; set; } = new List<EntityCardTag>();
    public ICollection<EntityCardComment> Comments { get; set; } = new List<EntityCardComment>();
}
