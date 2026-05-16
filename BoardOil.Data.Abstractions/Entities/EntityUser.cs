namespace BoardOil.Data.Abstractions.Entities;

public sealed class EntityUser : ISupportCreatedAt, ISupportUpdatedAt
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NormalisedEmail { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Standard;
    public UserIdentityType IdentityType { get; set; } = UserIdentityType.User;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; internal set; }

    public ICollection<EntityRefreshToken> RefreshTokens { get; set; } = new List<EntityRefreshToken>();
    public ICollection<EntityPersonalAccessToken> PersonalAccessTokens { get; set; } = new List<EntityPersonalAccessToken>();
    public ICollection<EntityBoardMember> BoardMemberships { get; set; } = new List<EntityBoardMember>();
    public ICollection<EntityCardComment> CardComments { get; set; } = new List<EntityCardComment>();
}
