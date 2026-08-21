namespace BoardOil.Contracts.OAuth;

public sealed record OAuthTokenAuditDto(
    int Id,
    DateTime OccurredAtUtc,
    string Outcome,
    string GrantType,
    string? ErrorCode,
    string? ErrorDescription,
    string? ErrorUri,
    string? PresentedTokenId,
    string? PresentedTokenFingerprint,
    string? IssuedRefreshTokenFingerprint,
    string? AuthorizationId,
    string? Subject,
    string? OAuthClientId,
    int? OAuthConnectionId,
    string? OAuthConnectionName,
    int? OwnerUserId,
    string? OwnerUserName,
    string? OAuthClientDisplayName,
    string? Resource,
    string? TraceIdentifier,
    string? UserAgent,
    DateTime CreatedAtUtc);

public sealed record OAuthTokenAuditListDto(
    IReadOnlyList<OAuthTokenAuditDto> Items,
    int Offset,
    int Limit,
    int TotalCount);
