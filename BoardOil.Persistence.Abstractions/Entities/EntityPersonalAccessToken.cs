namespace BoardOil.Persistence.Abstractions.Entities;

public sealed class EntityPersonalAccessToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public string TokenPrefix { get; set; } = string.Empty;
    public string ScopesCsv { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    public EntityUser User { get; set; } = null!;
}
