namespace BoardOil.Data.Abstractions.Entities;

public sealed class EntityOAuthTokenAudit
{
    public int Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string? ErrorDescription { get; set; }
    public string? ErrorUri { get; set; }
    public string GrantType { get; set; } = string.Empty;
    public string? RequestedScopes { get; set; }
    public string? PresentedTokenFingerprint { get; set; }
    public string? IssuedRefreshTokenFingerprint { get; set; }
    public string? AuthorizationId { get; set; }
    public string? OAuthClientId { get; set; }
    public int? OAuthConnectionId { get; set; }
    public string? OAuthConnectionName { get; set; }
    public int? OwnerUserId { get; set; }
    public string? OwnerUserName { get; set; }
    public string? OAuthClientDisplayName { get; set; }
    public string? Resource { get; set; }
    public string? TraceIdentifier { get; set; }
    public string? UserAgent { get; set; }
}
