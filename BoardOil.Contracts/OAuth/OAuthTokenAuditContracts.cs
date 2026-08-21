using System.Text.Json.Serialization;

namespace BoardOil.Contracts.OAuth;

public sealed record OAuthTokenAuditDto(
    int Id,
    DateTime OccurredAtUtc,
    string Outcome,
    string GrantType,
    string? RequestedScopes,
    string? ErrorCode,
    string? ErrorDescription,
    string? ErrorUri,
    string? PresentedTokenFingerprint,
    string? IssuedRefreshTokenFingerprint,
    string? AuthorizationId,
    [property: JsonPropertyName("oauthClientId")] string? OAuthClientId,
    [property: JsonPropertyName("oauthConnectionId")] int? OAuthConnectionId,
    [property: JsonPropertyName("oauthConnectionName")] string? OAuthConnectionName,
    int? OwnerUserId,
    string? OwnerUserName,
    [property: JsonPropertyName("oauthClientDisplayName")] string? OAuthClientDisplayName,
    string? Resource,
    string? TraceIdentifier,
    string? UserAgent);

public sealed record OAuthTokenAuditListDto(
    IReadOnlyList<OAuthTokenAuditDto> Items,
    int Offset,
    int Limit,
    int TotalCount);

public sealed record OAuthTokenAuditPurgeResultDto(
    int RetentionDays,
    DateTime CutoffUtc,
    int DeletedCount);
