using BoardOil.Contracts.Common;
using BoardOil.Contracts.OAuth;

namespace BoardOil.Abstractions.OAuth;

public interface IOAuthTokenAuditService
{
    Task<ApiResult<OAuthTokenAuditListDto>> ListAsync(
        int? offset,
        int? limit,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? outcome,
        string? grantType,
        int? connectionId,
        string? clientId,
        string? authorizationId,
        string? tokenFingerprint);

    Task<ApiResult<OAuthTokenAuditPurgeResultDto>> PurgeExpiredAsync(
        CancellationToken cancellationToken = default);

    Task RecordAsync(OAuthTokenAuditInput input);
}

public sealed record OAuthTokenAuditInput(
    string Outcome,
    string GrantType,
    IReadOnlyCollection<string> RequestedScopes,
    string? ErrorCode,
    string? ErrorDescription,
    string? ErrorUri,
    string? PresentedTokenFingerprint,
    string? IssuedRefreshTokenFingerprint,
    string? AuthorizationId,
    string? OAuthClientId,
    string? TraceIdentifier,
    string? UserAgent);

public static class OAuthTokenAuditOutcomes
{
    public const string Succeeded = "Succeeded";
    public const string Rejected = "Rejected";
}
