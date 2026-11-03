using System.Text.Json.Serialization;

namespace BoardOil.Mcp.Contracts;

public sealed record OAuthTokenAuditListInput(
    int Offset = 0,
    int Limit = 100,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    string? Outcome = null,
    string? GrantType = null,
    int? ConnectionId = null,
    string? ClientId = null,
    string? AuthorizationId = null,
    string? TokenFingerprint = null);

public sealed record McpOAuthTokenAudit(
    int Id,
    DateTime OccurredAtUtc,
    string Outcome,
    string GrantType,
    string? RequestedScopes,
    string? ErrorCode,
    string? PresentedTokenFingerprint,
    string? IssuedRefreshTokenFingerprint,
    string? AuthorizationId,
    [property: JsonPropertyName("oauthClientId")] string? OAuthClientId,
    [property: JsonPropertyName("oauthConnectionId")] int? OAuthConnectionId,
    string? TraceIdentifier);

public sealed record OAuthTokenAuditListOutput(
    IReadOnlyList<McpOAuthTokenAudit> Items,
    int Offset,
    int Limit,
    int TotalCount);
