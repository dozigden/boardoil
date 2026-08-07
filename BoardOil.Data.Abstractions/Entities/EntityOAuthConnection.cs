namespace BoardOil.Data.Abstractions.Entities;

public sealed class EntityOAuthConnection : ISupportCreatedAt, ISupportUpdatedAt
{
    public int Id { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NormalisedName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public int? ActiveGrantId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; internal set; }
    public DateTime? RevokedAtUtc { get; set; }
    public int? RevokedByUserId { get; set; }
    public string? RevokedByUserName { get; set; }

    public EntityUser User { get; set; } = null!;
    public EntityOAuthConnectionGrant? ActiveGrant { get; set; }
    public EntityUser? RevokedByUser { get; set; }
    public ICollection<EntityOAuthConnectionGrant> Grants { get; set; } = new List<EntityOAuthConnectionGrant>();
}
