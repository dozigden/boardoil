namespace BoardOil.Data.Abstractions.Entities;

public sealed class EntityMcpProjectConnection : ISupportCreatedAt, ISupportUpdatedAt
{
    public int Id { get; set; }
    public string PublicId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int ClientAccountId { get; set; }
    public string AllowedScopesCsv { get; set; } = string.Empty;
    public int? CreatedByUserId { get; set; }
    public string CreatedByUserName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; internal set; }
    public DateTime? RevokedAtUtc { get; set; }
    public int? RevokedByUserId { get; set; }
    public string? RevokedByUserName { get; set; }

    public EntityUser ClientAccount { get; set; } = null!;
    public EntityUser? CreatedByUser { get; set; }
    public EntityUser? RevokedByUser { get; set; }
}
