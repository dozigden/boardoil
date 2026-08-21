using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.OAuth;

public interface IOAuthTokenAuditRepository : IRepositoryBase<EntityOAuthTokenAudit>
{
    Task<int> CountAsync(OAuthTokenAuditQuery query);
    Task<IReadOnlyList<EntityOAuthTokenAudit>> ListAsync(
        OAuthTokenAuditQuery query,
        int offset,
        int limit);
}

public sealed record OAuthTokenAuditQuery(
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Outcome,
    string? GrantType,
    int? OAuthConnectionId,
    string? OAuthClientId,
    string? AuthorizationId,
    string? TokenFingerprint);
