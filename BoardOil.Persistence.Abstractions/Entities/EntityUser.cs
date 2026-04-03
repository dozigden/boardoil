namespace BoardOil.Persistence.Abstractions.Entities;

public sealed class EntityUser
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Standard;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<EntityRefreshToken> RefreshTokens { get; set; } = new List<EntityRefreshToken>();
    public ICollection<EntityPersonalAccessToken> PersonalAccessTokens { get; set; } = new List<EntityPersonalAccessToken>();
    public ICollection<EntityBoardMember> BoardMemberships { get; set; } = new List<EntityBoardMember>();
}
