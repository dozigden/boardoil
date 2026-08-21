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

    Task RecordAsync(OAuthTokenAuditInput input);
}

public sealed record OAuthTokenAuditInput(
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
    string? TraceIdentifier,
    string? UserAgent);

public static class OAuthTokenAuditOutcomes
{
    public const string Succeeded = "Succeeded";
    public const string Rejected = "Rejected";
}
