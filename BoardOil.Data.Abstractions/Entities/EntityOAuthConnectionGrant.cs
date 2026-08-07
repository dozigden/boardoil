namespace BoardOil.Data.Abstractions.Entities;

public sealed class EntityOAuthConnectionGrant : ISupportCreatedAt, ISupportUpdatedAt
{
    public int Id { get; set; }
    public int OAuthConnectionId { get; set; }
    public string OpenIddictApplicationId { get; set; } = string.Empty;
    public string OpenIddictAuthorizationId { get; set; } = string.Empty;
    public string OAuthClientId { get; set; } = string.Empty;
    public string OAuthClientDisplayName { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string ApprovedScopesCsv { get; set; } = string.Empty;
    public DateTime ApprovedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; internal set; }
    public DateTime? RevokedAtUtc { get; set; }
    public int? RevokedByUserId { get; set; }
    public string? RevokedByUserName { get; set; }
    public string? RevocationReason { get; set; }

    public EntityOAuthConnection OAuthConnection { get; set; } = null!;
    public EntityUser? RevokedByUser { get; set; }
}
